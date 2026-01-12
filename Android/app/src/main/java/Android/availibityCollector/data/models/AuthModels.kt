package Android.availibityCollector.data.models

import com.google.gson.annotations.SerializedName

data class LoginRequest(
    @SerializedName("email") val email: String,
    @SerializedName("password") val password: String,
    @SerializedName("rememberMe") val rememberMe: Boolean = false
)

data class RegisterRequest(
    @SerializedName("email") val email: String,
    @SerializedName("password") val password: String,
    @SerializedName("confirmPassword") val confirmPassword: String
)

data class AuthResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("message") val message: String?,
    @SerializedName("token") val token: String?,
    @SerializedName("user") val user: UserInfo?
)

data class UserInfo(
    @SerializedName("id") val id: String,
    @SerializedName("email") val email: String,
    @SerializedName("firstName") val firstName: String?,
    @SerializedName("lastName") val lastName: String?
)
