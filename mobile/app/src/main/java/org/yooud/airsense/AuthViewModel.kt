package org.yooud.airsense

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.google.firebase.auth.ktx.auth
import com.google.firebase.ktx.Firebase
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

class AuthViewModel(
    private val repo: AuthRepository = FirebaseAuthRepository()
) : ViewModel() {

    private val firebaseTokenProvider = FirebaseAuthTokenProvider()

    init {
        firebaseTokenProvider.startListening(object : FirebaseAuthTokenProvider.TokenListener {
            override fun onNewIdToken(token: String) {
                SessionManager.token = token
            }
            override fun onSignOut() {
                SessionManager.token = null
            }
        })
    }

    override fun onCleared() {
        super.onCleared()
        firebaseTokenProvider.stopListening()
    }

    val currentUser = repo.currentUser
        .stateIn(viewModelScope, SharingStarted.Lazily, null)

    fun signIn(email: String, pass: String) = viewModelScope.launch {
        repo.signIn(email, pass)
            .onFailure { /* TODO: show error */ }
        registerInApi()
    }

    fun signUp(email: String, pass: String) = viewModelScope.launch {
        repo.signUp(email, pass)
            .onFailure { /* TODO: show error */ }
        registerInApi()
    }

    fun signInWithGoogle(idToken: String) = viewModelScope.launch {
        repo.signInWithGoogle(idToken)
            .onFailure { /* TODO: show error */ }
        registerInApi()
    }

    private suspend fun registerInApi() {
        val response = ApiClient.service.register(RegisterRequest("EXAMPLE"))
        if (response.code() == 201) {
            Firebase.auth.currentUser!!
                .getIdToken(true)
                .addOnSuccessListener { result ->
                    SessionManager.token = result.token
                }
        }
    }

}
