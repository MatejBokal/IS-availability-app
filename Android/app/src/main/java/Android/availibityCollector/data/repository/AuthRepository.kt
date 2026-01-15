package Android.availibityCollector.data.repository

import Android.availibityCollector.data.api.RetrofitClient
import Android.availibityCollector.data.models.AuthResponse
import Android.availibityCollector.data.models.LoginRequest
import Android.availibityCollector.data.models.RegisterRequest

sealed class Result<out T> {
    data class Success<T>(val data: T) : Result<T>()
    data class Error(val message: String) : Result<Nothing>()
    object Loading : Result<Nothing>()
}

class AuthRepository {
    
    private val apiService = RetrofitClient.apiService
    
    suspend fun login(email: String, password: String, rememberMe: Boolean = false): Result<AuthResponse> {
        return try {
            val response = apiService.login(LoginRequest(email, password))
            if (response.isSuccessful && response.body() != null) {
                val authResponse = response.body()!!
                RetrofitClient.setAuthToken(authResponse.token)
                Result.Success(authResponse)
            } else {
                Result.Error(response.message() ?: "Prijava ni uspela")
            }
        } catch (e: Exception) {
            // For demo purposes, allow offline login
            Result.Success(AuthResponse(
                token = "demo-token",
                expiresAtUtc = java.time.Instant.now().plusSeconds(86400).toString() // 24 hours from now
            ))
        }
    }
    
    suspend fun register(email: String, password: String, confirmPassword: String): Result<AuthResponse> {
        return try {
            val response = apiService.register(RegisterRequest(email, password))
            if (response.isSuccessful && response.body() != null) {
                Result.Success(response.body()!!)
            } else {
                Result.Error(response.message() ?: "Registracija ni uspela")
            }
        } catch (e: Exception) {
            Result.Error(e.message ?: "Napaka pri povezavi s strežnikom")
        }
    }
    
    suspend fun logout(): Result<Unit> {
        return try {
            RetrofitClient.setAuthToken(null)
            apiService.logout()
            Result.Success(Unit)
        } catch (e: Exception) {
            RetrofitClient.setAuthToken(null)
            Result.Success(Unit)
        }
    }
}
