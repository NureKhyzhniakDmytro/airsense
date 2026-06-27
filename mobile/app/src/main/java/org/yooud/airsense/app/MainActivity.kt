package org.yooud.airsense.app

import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.View
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import com.google.android.material.snackbar.Snackbar
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.credentials.Credential
import androidx.credentials.CredentialManager
import androidx.credentials.CustomCredential
import androidx.credentials.GetCredentialRequest
import androidx.credentials.exceptions.GetCredentialException
import androidx.lifecycle.lifecycleScope
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential.Companion.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL
import kotlinx.coroutines.launch
import org.yooud.airsense.auth.AuthViewModel
import org.yooud.airsense.R
import org.yooud.airsense.ui.LoginScreen
import org.yooud.airsense.ui.ModernTheme
import org.yooud.airsense.ui.RegistrationScreen

class MainActivity : ComponentActivity() {
    private val authVm: AuthViewModel by viewModels()
    private lateinit var credentialManager: CredentialManager

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        credentialManager = CredentialManager.create(this)

        setContent {
            var showRegister by remember { mutableStateOf(false) }
            val user by authVm.currentUser.collectAsState(initial = null)
            val errorMessage by authVm.errorMessage.collectAsState(initial = null)
            val successMessage by authVm.successMessage.collectAsState(initial = null)
            val isLoading by authVm.isLoading.collectAsState(initial = false)
            val isApiSessionReady by authVm.isApiSessionReady.collectAsState(initial = false)

            LaunchedEffect(errorMessage) {
                errorMessage?.let { message ->
                    showErrorSnackBar(RuntimeException(message))
                    authVm.clearError()
                }
            }

            LaunchedEffect(successMessage) {
                successMessage?.let { message ->
                    showMessageSnackBar(message)
                    authVm.clearSuccess()
                }
            }

            LaunchedEffect(user?.uid, isApiSessionReady) {
                if (user != null && isApiSessionReady) {
                    startActivity(Intent(this@MainActivity, EnvironmentActivity::class.java))
                    finish()
                }
            }

            if (user != null && !isApiSessionReady) {
                WaitingForApiSessionScreen()
            } else if (user == null && showRegister) {
                RegistrationScreen(
                    isLoading = isLoading,
                    onRegister = { email, pass ->
                        authVm.signUp(email, pass)
                    },
                    onLogin = { showRegister = false },
                    onGoogleSignIn = { launchGoogleIdSignIn() }
                )
            } else if (user == null) {
                LoginScreen(
                    isLoading = isLoading,
                    onLogin = { email, pass -> authVm.signIn(email, pass) },
                    onGoogleSignIn = { launchGoogleIdSignIn() },
                    onRegister = { showRegister = true },
                    onForgotPassword = { email -> authVm.sendPasswordReset(email) }
                )
            }
        }

    }

    private fun launchGoogleIdSignIn() {
        val option = GetSignInWithGoogleOption
            .Builder(getString(R.string.default_web_client_id))
            .build()

        val request = GetCredentialRequest.Builder()
            .addCredentialOption(option)
            .build()

        lifecycleScope.launch {
            try {
                val resp = credentialManager.getCredential(this@MainActivity, request)
                handleGoogleCredential(resp.credential)
            } catch (e: GetCredentialException) {
                Log.e(TAG, "Credential Manager error: ${e.localizedMessage}")
                showErrorSnackBar(e)
            }
        }
    }

    private fun showErrorSnackBar(error: Throwable) {
        showMessageSnackBar(
            getString(
                R.string.sign_in_error,
                error.localizedMessage ?: error.message ?: ""
            )
        )
    }

    private fun showMessageSnackBar(message: String) {
        val root = findViewById<View>(android.R.id.content)
        Snackbar
            .make(
                root,
                message,
                Snackbar.LENGTH_LONG
            )
            .setAction(android.R.string.ok) { /* скрыть */ }
            .show()
    }

    private fun handleGoogleCredential(credential: Credential) {
        if (credential is CustomCredential &&
            credential.type == TYPE_GOOGLE_ID_TOKEN_CREDENTIAL) {
            val googleCred = GoogleIdTokenCredential.createFrom(credential.data)
            authVm.signInWithGoogle(googleCred.idToken)
        } else {
            Log.w(TAG, "Unexpected credential type: ${credential.type}")
        }
    }

    companion object {
        private const val TAG = "MainActivity"
    }
}

@Composable
private fun WaitingForApiSessionScreen() {
    ModernTheme {
        Surface(
            modifier = Modifier.fillMaxSize(),
            color = MaterialTheme.colorScheme.background
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center
            ) {
                CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
                Spacer(modifier = Modifier.height(16.dp))
                Text(
                    text = "Completing secure AirSense session...",
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onBackground,
                    textAlign = TextAlign.Center
                )
            }
        }
    }
}
