package Android.availibityCollector.data.api

import android.content.Context
import com.android.volley.Request
import com.android.volley.RequestQueue
import com.android.volley.toolbox.JsonArrayRequest
import com.android.volley.toolbox.JsonObjectRequest
import com.android.volley.toolbox.Volley
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import org.json.JSONObject
import Android.availibityCollector.data.TokenManager
import Android.availibityCollector.data.models.Worker
import Android.availibityCollector.data.models.AvailabilityModels.*
import Android.availibityCollector.data.models.AuthModels.*

/**
 * Volley client for REST API communication with JWT authentication
 * Implements all Worker functionality endpoints
 */
class VolleyClient(context: Context) {
    
    companion object {
        // For emulator use: 10.0.2.2 (maps to localhost)
        // For physical device: use your computer's IP address
        // Port 5180 matches the ASP.NET backend launchSettings.json
        private const val BASE_URL = "http://10.0.2.2:5180/api/"
        
        @Volatile
        private var INSTANCE: VolleyClient? = null
        
        fun getInstance(context: Context): VolleyClient {
            return INSTANCE ?: synchronized(this) {
                INSTANCE ?: VolleyClient(context.applicationContext).also {
                    INSTANCE = it
                }
            }
        }
    }
    
    private val requestQueue: RequestQueue = Volley.newRequestQueue(context.applicationContext)
    private val gson = Gson()
    private val tokenManager = TokenManager(context)
    
    /**
     * Helper to add JWT token to request headers
     */
    private fun getAuthHeaders(): Map<String, String> {
        val headers = mutableMapOf<String, String>()
        headers["Content-Type"] = "application/json"
        tokenManager.getToken()?.let { token ->
            headers["Authorization"] = "Bearer $token"
        }
        return headers
    }
    
    /**
     * Authentication: Login
     */
    fun login(
        email: String,
        password: String,
        onSuccess: (AuthResponse) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}auth/login"
        
        val jsonBody = JSONObject().apply {
            put("email", email)
            put("password", password)
        }
        
        val request = object : JsonObjectRequest(
            Request.Method.POST,
            url,
            jsonBody,
            { response ->
                try {
                    val authResponse: AuthResponse = gson.fromJson(response.toString(), AuthResponse::class.java)
                    // Save token
                    tokenManager.saveToken(authResponse.token, authResponse.expiresAtUtc, email)
                    onSuccess(authResponse)
                } catch (e: Exception) {
                    onError("Error parsing response: ${e.message}")
                }
            },
            { error ->
                val errorMessage = when {
                    error.networkResponse != null -> {
                        val errorBody = String(error.networkResponse.data ?: ByteArray(0))
                        "HTTP ${error.networkResponse.statusCode}: $errorBody"
                    }
                    error.message != null -> error.message!!
                    else -> "Unknown network error"
                }
                onError(errorMessage)
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val headers = super.getHeaders() ?: mutableMapOf()
                headers["Content-Type"] = "application/json"
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * Authentication: Register
     */
    fun register(
        email: String,
        password: String,
        onSuccess: (String) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}auth/register"
        
        val jsonBody = JSONObject().apply {
            put("email", email)
            put("password", password)
        }
        
        val request = object : JsonObjectRequest(
            Request.Method.POST,
            url,
            jsonBody,
            { response ->
                try {
                    val message = response.optString("message", "Registration successful")
                    onSuccess(message)
                } catch (e: Exception) {
                    onError("Error parsing response: ${e.message}")
                }
            },
            { error ->
                val errorMessage = when {
                    error.networkResponse != null -> {
                        val errorBody = String(error.networkResponse.data ?: ByteArray(0))
                        "HTTP ${error.networkResponse.statusCode}: $errorBody"
                    }
                    error.message != null -> error.message!!
                    else -> "Unknown network error"
                }
                onError(errorMessage)
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val headers = super.getHeaders() ?: mutableMapOf()
                headers["Content-Type"] = "application/json"
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * Get unlocked months available for submission
     */
    fun getUnlockedMonths(
        onSuccess: (List<MonthDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}months/unlocked"
        
        val request = object : JsonArrayRequest(
            Request.Method.GET,
            url,
            null,
            { response ->
                try {
                    val type = object : TypeToken<List<MonthDto>>() {}.type
                    val months: List<MonthDto> = gson.fromJson(response.toString(), type)
                    onSuccess(months)
                } catch (e: Exception) {
                    onError("Error parsing response: ${e.message}")
                }
            },
            { error ->
                val errorMessage = when {
                    error.networkResponse != null -> {
                        val errorBody = String(error.networkResponse.data ?: ByteArray(0))
                        "HTTP ${error.networkResponse.statusCode}: $errorBody"
                    }
                    error.message != null -> error.message!!
                    else -> "Unknown network error"
                }
                onError(errorMessage)
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val headers = super.getHeaders() ?: mutableMapOf()
                getAuthHeaders().forEach { (key, value) -> headers[key] = value }
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * Get month info to check if it's locked
     */
    fun getMonthInfo(
        monthKey: String,
        onSuccess: (MonthDto) -> Unit,
        onError: (String) -> Unit
    ) {
        // Get from unlocked months list
        getUnlockedMonths(
            onSuccess = { months ->
                val month = months.find { it.monthKey == monthKey }
                if (month != null) {
                    onSuccess(month)
                } else {
                    // Month not in unlocked list, check if it exists and is locked
                    // For now, assume it's locked if not in unlocked list
                    onError("Month not found or locked")
                }
            },
            onError = onError
        )
    }
    
    /**
     * Get my availability for a specific month
     */
    fun getMyAvailability(
        monthKey: String,
        onSuccess: (AvailabilitySubmissionDto) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}availability/my?monthKey=$monthKey"
        
        val request = object : JsonObjectRequest(
            Request.Method.GET,
            url,
            null,
            { response ->
                try {
                    val submission: AvailabilitySubmissionDto = gson.fromJson(response.toString(), AvailabilitySubmissionDto::class.java)
                    onSuccess(submission)
                } catch (e: Exception) {
                    onError("Error parsing response: ${e.message}")
                }
            },
            { error ->
                val errorMessage = when {
                    error.networkResponse?.statusCode == 404 -> "No submission found for this month"
                    error.networkResponse != null -> {
                        val errorBody = String(error.networkResponse.data ?: ByteArray(0))
                        "HTTP ${error.networkResponse.statusCode}: $errorBody"
                    }
                    error.message != null -> error.message!!
                    else -> "Unknown network error"
                }
                onError(errorMessage)
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val headers = super.getHeaders() ?: mutableMapOf()
                getAuthHeaders().forEach { (key, value) -> headers[key] = value }
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * Create availability submission
     */
    fun createAvailabilitySubmission(
        request: CreateAvailabilityRequest,
        onSuccess: (Int) -> Unit,  // Returns submissionId
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}availability/my/submissions"
        
        val jsonBody = JSONObject().apply {
            put("monthKey", request.monthKey)
            val entriesArray = org.json.JSONArray()
            request.entries.forEach { entry ->
                val entryObj = JSONObject().apply {
                    put("date", entry.date)
                    put("type", entry.type)
                    entry.startTime?.let { put("startTime", it) }
                    entry.endTime?.let { put("endTime", it) }
                }
                entriesArray.put(entryObj)
            }
            put("entries", entriesArray)
        }
        
        val volleyRequest = object : JsonObjectRequest(
            Request.Method.POST,
            url,
            jsonBody,
            { response ->
                try {
                    val submissionId = response.optInt("submissionId", 0)
                    onSuccess(submissionId)
                } catch (e: Exception) {
                    onError("Error parsing response: ${e.message}")
                }
            },
            { error ->
                val errorMessage = when {
                    error.networkResponse != null -> {
                        val errorBody = String(error.networkResponse.data ?: ByteArray(0))
                        try {
                            val errorJson = JSONObject(errorBody)
                            errorJson.optString("error", "Unknown error")
                        } catch (e: Exception) {
                            "HTTP ${error.networkResponse.statusCode}: $errorBody"
                        }
                    }
                    error.message != null -> error.message!!
                    else -> "Unknown network error"
                }
                onError(errorMessage)
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val headers = super.getHeaders() ?: mutableMapOf()
                getAuthHeaders().forEach { (key, value) -> headers[key] = value }
                return headers
            }
        }
        
        requestQueue.add(volleyRequest)
    }
    
    /**
     * Update availability submission
     */
    fun updateAvailabilitySubmission(
        submissionId: Int,
        request: CreateAvailabilityRequest,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}availability/my/submissions/$submissionId"
        
        val jsonBody = JSONObject().apply {
            put("monthKey", request.monthKey)
            val entriesArray = org.json.JSONArray()
            request.entries.forEach { entry ->
                val entryObj = JSONObject().apply {
                    put("date", entry.date)
                    put("type", entry.type)
                    entry.startTime?.let { put("startTime", it) }
                    entry.endTime?.let { put("endTime", it) }
                }
                entriesArray.put(entryObj)
            }
            put("entries", entriesArray)
        }
        
        val volleyRequest = object : JsonObjectRequest(
            Request.Method.PUT,
            url,
            jsonBody,
            { _ ->
                onSuccess()
            },
            { error ->
                val errorMessage = when {
                    error.networkResponse != null -> {
                        val errorBody = String(error.networkResponse.data ?: ByteArray(0))
                        try {
                            val errorJson = JSONObject(errorBody)
                            errorJson.optString("error", "Unknown error")
                        } catch (e: Exception) {
                            "HTTP ${error.networkResponse.statusCode}: $errorBody"
                        }
                    }
                    error.message != null -> error.message!!
                    else -> "Unknown network error"
                }
                onError(errorMessage)
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val headers = super.getHeaders() ?: mutableMapOf()
                getAuthHeaders().forEach { (key, value) -> headers[key] = value }
                return headers
            }
        }
        
        requestQueue.add(volleyRequest)
    }
    
    /**
     * Get all my submissions (for history)
     * Note: This endpoint doesn't exist yet, but we can get it by trying each month
     * For now, we'll use a workaround by getting unlocked months and checking each
     */
    fun getMySubmissions(
        onSuccess: (List<AvailabilitySubmissionDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        // First get unlocked months, then try to get submission for each
        getUnlockedMonths(
            onSuccess = { months ->
                val submissions = mutableListOf<AvailabilitySubmissionDto>()
                var completed = 0
                val total = months.size
                
                if (total == 0) {
                    onSuccess(emptyList())
                    return@getUnlockedMonths
                }
                
                months.forEach { month ->
                    getMyAvailability(
                        monthKey = month.monthKey,
                        onSuccess = { submission ->
                            submissions.add(submission)
                            completed++
                            if (completed == total) {
                                onSuccess(submissions)
                            }
                        },
                        onError = { _ ->
                            // Ignore 404 errors (no submission for this month)
                            completed++
                            if (completed == total) {
                                onSuccess(submissions)
                            }
                        }
                    )
                }
            },
            onError = onError
        )
    }
    
    /**
     * Update notification preference
     */
    fun updateNotificationPreference(
        enableNotifications: Boolean,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "http://10.0.2.2:5180/nastavitve/update-notification-preference"
        
        val jsonBody = JSONObject().apply {
            put("enableNotifications", enableNotifications)
        }
        
        val request = object : JsonObjectRequest(
            Request.Method.POST,
            url,
            jsonBody,
            { _ ->
                onSuccess()
            },
            { error ->
                val errorMessage = when {
                    error.networkResponse != null -> {
                        val errorBody = String(error.networkResponse.data ?: ByteArray(0))
                        "HTTP ${error.networkResponse.statusCode}: $errorBody"
                    }
                    error.message != null -> error.message!!
                    else -> "Unknown network error"
                }
                onError(errorMessage)
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val headers = super.getHeaders() ?: mutableMapOf()
                getAuthHeaders().forEach { (key, value) -> headers[key] = value }
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * READ operation - Get all workers from the REST API
     * Workers can view the list of all colleagues
     */
    fun getWorkers(
        onSuccess: (List<Worker>) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}workersapi"
        
        val request = object : JsonArrayRequest(
            Request.Method.GET,
            url,
            null,
            { response ->
                try {
                    val type = object : TypeToken<List<Worker>>() {}.type
                    val workers: List<Worker> = gson.fromJson(response.toString(), type)
                    onSuccess(workers)
                } catch (e: Exception) {
                    onError("Error parsing response: ${e.message}")
                }
            },
            { error ->
                val errorMessage = when {
                    error.networkResponse != null -> {
                        "HTTP ${error.networkResponse.statusCode}: ${String(error.networkResponse.data ?: ByteArray(0))}"
                    }
                    error.message != null -> error.message!!
                    else -> "Unknown network error"
                }
                onError(errorMessage)
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val headers = super.getHeaders() ?: mutableMapOf()
                getAuthHeaders().forEach { (key, value) -> headers[key] = value }
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * Clear token on logout
     */
    fun logout() {
        tokenManager.clearToken()
    }
    
    /**
     * Cancel all pending requests
     */
    fun cancelAllRequests() {
        requestQueue.cancelAll { true }
    }
}
