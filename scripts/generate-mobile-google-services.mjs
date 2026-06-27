#!/usr/bin/env node

import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const rootDir = path.resolve(new URL('..', import.meta.url).pathname);
const dotenvPath = path.join(rootDir, '.env');
const serviceAccountPath =
  process.env.FIREBASE_CREDENTIALS_JSON_FILE ||
  readDotenv(dotenvPath).FIREBASE_CREDENTIALS_JSON_FILE ||
  '/opt/airsense/secrets/service.json';
const outputPath =
  process.env.MOBILE_GOOGLE_SERVICES_JSON ||
  path.join(rootDir, 'mobile/app/google-services.json');
const packageName = process.env.MOBILE_FIREBASE_PACKAGE || 'org.yooud.airsense';
const displayName = process.env.MOBILE_FIREBASE_DISPLAY_NAME || 'AirSense Android';
const shouldRegisterDebugSha = process.env.MOBILE_FIREBASE_REGISTER_DEBUG_SHA !== 'false';

function readDotenv(filePath) {
  if (!fs.existsSync(filePath)) return {};
  return Object.fromEntries(
    fs
      .readFileSync(filePath, 'utf8')
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line && !line.startsWith('#') && line.includes('='))
      .map((line) => {
        const [key, ...parts] = line.split('=');
        return [key.trim(), parts.join('=').trim().replace(/^['"]|['"]$/g, '')];
      }),
  );
}

function base64Url(value) {
  const buffer = Buffer.isBuffer(value)
    ? value
    : Buffer.from(typeof value === 'string' ? value : JSON.stringify(value));
  return buffer.toString('base64').replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
}

function normalizeShaHash(value) {
  return value.replace(/[^a-fA-F0-9]/g, '').toUpperCase();
}

function readDebugSigningCertificates() {
  const explicit = [
    ['SHA_1', process.env.MOBILE_FIREBASE_SHA1],
    ['SHA_256', process.env.MOBILE_FIREBASE_SHA256],
  ]
    .filter(([, value]) => Boolean(value))
    .map(([certType, value]) => ({ certType, shaHash: normalizeShaHash(value) }));
  if (explicit.length > 0 || !shouldRegisterDebugSha) return explicit;

  try {
    const mobileDir = path.join(rootDir, 'mobile');
    const gradlewName = process.platform === 'win32' ? 'gradlew.bat' : 'gradlew';
    const gradlewPath = path.join(mobileDir, gradlewName);
    let canRunWrapper = false;
    try {
      fs.accessSync(gradlewPath, fs.constants.X_OK);
      canRunWrapper = true;
    } catch {
      canRunWrapper = false;
    }
    const gradleCommand = canRunWrapper ? `./${gradlewName}` : 'gradle';
    const output = execFileSync(gradleCommand, ['signingReport', '--no-daemon'], {
      cwd: mobileDir,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    const debugSection = output.split(/\r?\n\r?\n/).find((section) => section.includes('Variant: debug'));
    if (!debugSection) return [];
    const sha1 = debugSection.match(/SHA1:\s*([A-Fa-f0-9:]+)/)?.[1];
    const sha256 = debugSection.match(/SHA-256:\s*([A-Fa-f0-9:]+)/)?.[1];
    return [
      sha1 && { certType: 'SHA_1', shaHash: normalizeShaHash(sha1) },
      sha256 && { certType: 'SHA_256', shaHash: normalizeShaHash(sha256) },
    ].filter(Boolean);
  } catch (error) {
    console.warn(`Could not read debug signing certificates: ${error.message}`);
    return [];
  }
}

async function jsonRequest(label, url, options = {}) {
  const response = await fetch(url, options);
  const text = await response.text();
  if (!response.ok) {
    throw new Error(`${label} failed with HTTP ${response.status}: ${text.slice(0, 1000)}`);
  }
  return text ? JSON.parse(text) : {};
}

async function getAccessToken(serviceAccount) {
  const now = Math.floor(Date.now() / 1000);
  const header = { alg: 'RS256', typ: 'JWT' };
  const claims = {
    iss: serviceAccount.client_email,
    scope: 'https://www.googleapis.com/auth/cloud-platform https://www.googleapis.com/auth/firebase',
    aud: 'https://oauth2.googleapis.com/token',
    iat: now,
    exp: now + 3600,
  };
  const signingInput = `${base64Url(header)}.${base64Url(claims)}`;
  const signer = crypto.createSign('RSA-SHA256');
  signer.update(signingInput);
  signer.end();
  const assertion = `${signingInput}.${base64Url(signer.sign(serviceAccount.private_key))}`;
  const token = await jsonRequest('OAuth token', 'https://oauth2.googleapis.com/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer',
      assertion,
    }),
  });
  return token.access_token;
}

async function main() {
  if (!fs.existsSync(serviceAccountPath)) {
    throw new Error(`Firebase service account was not found at ${serviceAccountPath}`);
  }
  const serviceAccount = JSON.parse(fs.readFileSync(serviceAccountPath, 'utf8'));
  if (!serviceAccount.client_email || !serviceAccount.private_key || !serviceAccount.project_id) {
    throw new Error('Firebase service account JSON is missing required fields');
  }

  const accessToken = await getAccessToken(serviceAccount);
  const headers = {
    Authorization: `Bearer ${accessToken}`,
    'Content-Type': 'application/json',
  };
  const apiRoot = 'https://firebase.googleapis.com/v1beta1';
  const projectBase = `${apiRoot}/projects/${serviceAccount.project_id}`;
  const listApps = async () => {
    const payload = await jsonRequest('List Android apps', `${projectBase}/androidApps`, { headers });
    return payload.apps || [];
  };

  let apps = await listApps();
  let app = apps.find((candidate) => candidate.packageName === packageName);
  if (!app) {
    const operation = await jsonRequest('Create Android app', `${projectBase}/androidApps`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ packageName, displayName }),
    });
    if (operation.name) {
      for (let attempt = 0; attempt < 30; attempt += 1) {
        const current = await jsonRequest('Poll Android app creation', `${apiRoot}/${operation.name}`, {
          headers,
        });
        if (current.done) {
          if (current.error) {
            throw new Error(`Android app creation failed: ${JSON.stringify(current.error).slice(0, 1000)}`);
          }
          break;
        }
        await new Promise((resolve) => setTimeout(resolve, 2000));
      }
    }
    apps = await listApps();
    app = apps.find((candidate) => candidate.packageName === packageName);
  }
  if (!app) {
    throw new Error(`Android app ${packageName} was not found in Firebase project`);
  }

  const requestedCertificates = readDebugSigningCertificates();
  if (requestedCertificates.length > 0) {
    const existing = await jsonRequest('List Android app SHA certificates', `${apiRoot}/${app.name}/sha`, {
      headers,
    });
    const existingKeys = new Set(
      (existing.certificates || []).map(
        (certificate) => `${certificate.certType}:${normalizeShaHash(certificate.shaHash)}`,
      ),
    );
    for (const certificate of requestedCertificates) {
      const key = `${certificate.certType}:${certificate.shaHash}`;
      if (existingKeys.has(key)) continue;
      await jsonRequest('Create Android app SHA certificate', `${apiRoot}/${app.name}/sha`, {
        method: 'POST',
        headers,
        body: JSON.stringify(certificate),
      });
      console.log(`Registered ${certificate.certType} for ${packageName}`);
    }
  }

  const config = await jsonRequest('Fetch Android app config', `${apiRoot}/${app.name}/config`, {
    headers,
  });
  const decoded = Buffer.from(config.configFileContents || '', 'base64');
  const parsed = JSON.parse(decoded.toString('utf8'));
  const clientPackage = parsed.client?.[0]?.client_info?.android_client_info?.package_name;
  if (clientPackage !== packageName) {
    throw new Error(`Fetched config package mismatch: ${clientPackage || 'missing'}`);
  }

  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, decoded);
  console.log(`Wrote ${outputPath}`);
  console.log(`Firebase project: ${serviceAccount.project_id}`);
  console.log(`Android package: ${packageName}`);
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
