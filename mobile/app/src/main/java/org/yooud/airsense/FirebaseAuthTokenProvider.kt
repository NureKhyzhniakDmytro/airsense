package org.yooud.airsense

import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.ktx.auth
import com.google.firebase.ktx.Firebase

class FirebaseAuthTokenProvider {

    private val auth: FirebaseAuth = Firebase.auth

    interface TokenListener {
        fun onNewIdToken(token: String)
        fun onSignOut()
    }

    private var listener: TokenListener? = null

    private val authStateListener = FirebaseAuth.AuthStateListener { firebaseAuth ->
        val user = firebaseAuth.currentUser
        if (user != null) {
            user.getIdToken(false)
                .addOnSuccessListener { result ->
                    result.token?.let { idToken ->
                        listener?.onNewIdToken(idToken)
                    }
                }
                .addOnFailureListener { }
        } else {
            listener?.onSignOut()
        }
    }

    fun startListening(tokenListener: TokenListener) {
        listener = tokenListener
        auth.addAuthStateListener(authStateListener)
    }

    fun stopListening() {
        auth.removeAuthStateListener(authStateListener)
        listener = null
    }
}
