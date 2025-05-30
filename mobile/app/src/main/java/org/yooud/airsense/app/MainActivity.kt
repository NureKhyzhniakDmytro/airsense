package org.yooud.airsense.app

import android.os.Bundle
import android.util.Log
import android.view.View
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import com.google.android.material.snackbar.Snackbar
import androidx.compose.runtime.*
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
import org.yooud.airsense.AuthViewModel
import org.yooud.airsense.R
import org.yooud.airsense.ui.HomeScreen
import org.yooud.airsense.ui.LoginScreen
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

            if (user != null) {
                HomeScreen(user!!)
            } else if (showRegister) {
                RegistrationScreen(
                    onRegister = { email, pass ->
                        authVm.signUp(email, pass)
                        showRegister = false
                    },
                    onLogin = { showRegister = false },
                    onGoogleSignIn = { launchGoogleIdSignIn() }
                )
            } else {
                LoginScreen(
                    onLogin = { email, pass -> authVm.signIn(email, pass) },
                    onGoogleSignIn = { launchGoogleIdSignIn() },
                    onRegister = { showRegister = true },
                    onForgotPassword = {}
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
        val root = findViewById<View>(android.R.id.content)
        Snackbar
            .make(
                root,
                getString(
                    R.string.sign_in_error,
                    error.localizedMessage ?: error.message ?: ""
                ),
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
