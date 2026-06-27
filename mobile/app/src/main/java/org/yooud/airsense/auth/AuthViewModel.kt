package org.yooud.airsense.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.google.firebase.auth.ktx.auth
import com.google.firebase.ktx.Firebase
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.tasks.await
import org.yooud.airsense.fcm.FirebaseMessagingRepository
import org.yooud.airsense.network.ApiClient
import org.yooud.airsense.models.RegisterRequest

class AuthViewModel(
    private val repo: AuthRepository = FirebaseAuthRepository()
) : ViewModel() {

    private val firebaseTokenProvider = FirebaseAuthTokenProvider()
    private val sessionMutex = Mutex()

    val currentUser = repo.currentUser
        .stateIn(viewModelScope, SharingStarted.Companion.Lazily, null)

    private val _errorMessage = MutableStateFlow<String?>(null)
    val errorMessage: StateFlow<String?> = _errorMessage

    private val _successMessage = MutableStateFlow<String?>(null)
    val successMessage: StateFlow<String?> = _successMessage

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading

    private val _isApiSessionReady = MutableStateFlow(false)
    val isApiSessionReady: StateFlow<Boolean> = _isApiSessionReady

    init {
        firebaseTokenProvider.startListening(object : FirebaseAuthTokenProvider.TokenListener {
            override fun onNewIdToken(token: String) {
                SessionManager.token = token
            }
            override fun onSignOut() {
                SessionManager.token = null
            }
        })

        viewModelScope.launch {
            repo.currentUser.collectLatest { user ->
                if (user == null) {
                    _isApiSessionReady.value = false
                    return@collectLatest
                }

                ensureApiSession()
            }
        }
    }

    override fun onCleared() {
        super.onCleared()
        firebaseTokenProvider.stopListening()
    }

    fun signIn(email: String, pass: String) = authenticate {
        repo.signIn(email, pass)
    }

    fun signUp(email: String, pass: String) = authenticate {
        repo.signUp(email, pass)
    }

    fun signInWithGoogle(idToken: String) = authenticate {
        repo.signInWithGoogle(idToken)
    }

    fun signOut() {
        repo.signOut()
    }

    fun sendPasswordReset(email: String) = viewModelScope.launch {
        _isLoading.value = true
        _errorMessage.value = null
        _successMessage.value = null
        repo.sendPasswordReset(email)
            .onSuccess {
                _successMessage.value = "Password reset email sent."
            }
            .onFailure { error ->
                _errorMessage.value = error.localizedMessage ?: error.message ?: "Unable to send password reset email."
            }
        _isLoading.value = false
    }

    fun clearError() {
        _errorMessage.value = null
    }

    fun clearSuccess() {
        _successMessage.value = null
    }

    private fun authenticate(action: suspend () -> Result<User>) = viewModelScope.launch {
        _isLoading.value = true
        _errorMessage.value = null
        _successMessage.value = null

        try {
            action()
                .onSuccess { ensureApiSession() }
                .onFailure { error ->
                    _errorMessage.value = error.localizedMessage ?: error.message ?: "Unable to sign in."
                }
        } catch (error: Exception) {
            _errorMessage.value = error.localizedMessage ?: error.message ?: "Unable to register mobile session."
        } finally {
            _isLoading.value = false
        }
    }

    private suspend fun ensureApiSession() = sessionMutex.withLock {
        val firebaseUser = Firebase.auth.currentUser
        if (firebaseUser == null) {
            _isApiSessionReady.value = false
            return
        }

        if (_isApiSessionReady.value && SessionManager.token != null) return

        _isLoading.value = true
        _errorMessage.value = null
        _successMessage.value = null

        try {
            registerInApi()
            _isApiSessionReady.value = true
        } catch (error: Exception) {
            _isApiSessionReady.value = false
            _errorMessage.value = error.localizedMessage ?: error.message ?: "Unable to complete AirSense API session."
            repo.signOut()
        } finally {
            _isLoading.value = false
        }
    }

    private suspend fun registerInApi() {
        val user = Firebase.auth.currentUser
            ?: error("Firebase user is not available")
        val currentToken = user.getIdToken(false).await().token
            ?: error("Firebase did not return an ID token")
        SessionManager.token = currentToken

        val fcmToken = runCatching { FirebaseMessagingRepository().fetchFcmToken() }.getOrNull()
        val response = ApiClient.service.register(RegisterRequest(fcmToken))
        if (response.isSuccessful) {
            val token = user.getIdToken(true).await().token
                ?: error("Firebase did not return an ID token")
            SessionManager.token = token
            return
        }

        val errorBody = response.errorBody()?.string()
        error(errorBody ?: "Unable to register the mobile session in AirSense API (${response.code()}).")
    }

}
