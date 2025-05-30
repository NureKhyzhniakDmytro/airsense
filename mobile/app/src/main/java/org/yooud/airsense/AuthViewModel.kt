package org.yooud.airsense

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

class AuthViewModel(
    private val repo: AuthRepository = FirebaseAuthRepository()
) : ViewModel() {
    val currentUser = repo.currentUser
        .stateIn(viewModelScope, SharingStarted.Lazily, null)

    fun signIn(email: String, pass: String) = viewModelScope.launch {
        repo.signIn(email, pass)
            .onFailure { /* TODO: show error */ }
    }

    fun signUp(email: String, pass: String) = viewModelScope.launch {
        repo.signUp(email, pass)
            .onFailure { /* TODO: show error */ }
    }

    fun signInWithGoogle(idToken: String) = viewModelScope.launch {
        repo.signInWithGoogle(idToken)
            .onFailure { /* TODO: show error */ }
    }

}
