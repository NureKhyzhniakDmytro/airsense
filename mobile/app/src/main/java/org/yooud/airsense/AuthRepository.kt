package org.yooud.airsense

import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.GoogleAuthProvider
import com.google.firebase.auth.ktx.auth
import com.google.firebase.ktx.Firebase
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.tasks.await

data class User(val uid: String, val email: String)

interface AuthRepository {
    suspend fun signIn(email: String, pass: String): Result<User>
    suspend fun signUp(email: String, pass: String): Result<User>
    suspend fun signInWithGoogle(idToken: String): Result<User>
    fun signOut()
    val currentUser: Flow<User?>
}

class FirebaseAuthRepository(
    private val auth: FirebaseAuth = Firebase.auth
) : AuthRepository {
    override val currentUser: Flow<User?> = callbackFlow {
        val listener = FirebaseAuth.AuthStateListener {
            trySend(it.currentUser?.toUser())
        }
        auth.addAuthStateListener(listener)
        awaitClose { auth.removeAuthStateListener(listener) }
    }

    override suspend fun signIn(email: String, pass: String): Result<User> = try {
        val result = auth.signInWithEmailAndPassword(email, pass).await()
        Result.success(result.user!!.toUser())
    } catch(e: Exception) { Result.failure(e) }

    override suspend fun signUp(email: String, pass: String): Result<User> = try {
        val result = auth.createUserWithEmailAndPassword(email, pass).await()
        Result.success(result.user!!.toUser())
    } catch(e: Exception) { Result.failure(e) }

    override suspend fun signInWithGoogle(idToken: String): Result<User> = try {
        val credential = GoogleAuthProvider.getCredential(idToken, null)
        val result = auth.signInWithCredential(credential).await()
        Result.success(result.user!!.toUser())
    } catch (e: Exception) {
        Result.failure(e)
    }

    override fun signOut() = auth.signOut()

    private fun com.google.firebase.auth.FirebaseUser.toUser() =
        User(uid, email ?: "")
}
