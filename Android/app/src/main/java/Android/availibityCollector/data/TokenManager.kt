package Android.availibityCollector.data

import android.content.Context
import android.content.SharedPreferences

class TokenManager(context: Context) {
    private val prefs: SharedPreferences = context.getSharedPreferences(
        "auth_prefs",
        Context.MODE_PRIVATE
    )
    
    companion object {
        private const val KEY_TOKEN = "jwt_token"
        private const val KEY_EXPIRES_AT = "token_expires_at"
        private const val KEY_USER_EMAIL = "user_email"
    }
    
    fun saveToken(token: String, expiresAtUtc: String?, email: String?) {
        prefs.edit().apply {
            putString(KEY_TOKEN, token)
            expiresAtUtc?.let { putString(KEY_EXPIRES_AT, it) }
            email?.let { putString(KEY_USER_EMAIL, it) }
            apply()
        }
    }
    
    fun getToken(): String? {
        return prefs.getString(KEY_TOKEN, null)
    }
    
    fun getExpiresAt(): String? {
        return prefs.getString(KEY_EXPIRES_AT, null)
    }
    
    fun getUserEmail(): String? {
        return prefs.getString(KEY_USER_EMAIL, null)
    }
    
    fun clearToken() {
        prefs.edit().apply {
            remove(KEY_TOKEN)
            remove(KEY_EXPIRES_AT)
            remove(KEY_USER_EMAIL)
            apply()
        }
    }
    
    fun isTokenValid(): Boolean {
        val token = getToken()
        val expiresAt = getExpiresAt()
        
        if (token == null) return false
        
        // If no expiration date, assume valid (for now)
        if (expiresAt == null) return true
        
        // Check if token is expired
        return try {
            val expiresAtMillis = java.time.Instant.parse(expiresAt).toEpochMilli()
            System.currentTimeMillis() < expiresAtMillis
        } catch (e: Exception) {
            true // If parsing fails, assume valid
        }
    }
}
