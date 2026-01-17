package Android.availibityCollector.data.api

import android.content.Context
import com.android.volley.Request
import com.android.volley.RequestQueue
import com.android.volley.Response
import com.android.volley.toolbox.JsonArrayRequest
import com.android.volley.toolbox.JsonObjectRequest
import com.android.volley.toolbox.StringRequest
import com.android.volley.toolbox.Volley
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import org.json.JSONObject
import Android.availibityCollector.data.TokenManager
import Android.availibityCollector.data.models.Worker
import Android.availibityCollector.data.models.AuthResponse
import Android.availibityCollector.data.models.MonthDto
import Android.availibityCollector.data.models.AvailabilitySubmissionDto
import Android.availibityCollector.data.models.CreateAvailabilityRequest
import Android.availibityCollector.data.models.AvailabilityEntryDto

/**
 * Custom StringRequest that supports custom headers
 */
private class AuthenticatedStringRequest(
    method: Int,
    url: String,
    private val authHeaders: Map<String, String>,
    listener: Response.Listener<String>,
    errorListener: Response.ErrorListener
) : StringRequest(method, url, listener, errorListener) {
    override fun getHeaders(): MutableMap<String, String> {
        val superHeaders = super.getHeaders() ?: emptyMap()
        val headers = mutableMapOf<String, String>()
        headers.putAll(superHeaders)
        authHeaders.forEach { (key, value) -> headers[key] = value }
        return headers
    }
}

/**
 * Volley client for REST API communication with JWT authentication
 * Implements all Worker functionality endpoints
 */
class VolleyClient(context: Context) {
    
    companion object {
        // Deployed backend URL
        private const val BASE_URL = "https://availabilityapp-api-ascabgdzc2cvb7aw.italynorth-01.azurewebsites.net/api/"
        
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
        val token = tokenManager.getToken()
        if (token != null && token.isNotBlank()) {
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
                    // ASP.NET Core serializes to camelCase by default
                    // Response should be: {"token": "...", "expiresAtUtc": "2026-01-15T12:00:00Z"}
                    val authResponse: AuthResponse = gson.fromJson(response.toString(), AuthResponse::class.java)
                    
                    if (authResponse.token.isNullOrBlank()) {
                        onError("Invalid response: token is missing")
                    } else {
                        // Save token
                        tokenManager.saveToken(
                            authResponse.token, 
                            authResponse.expiresAtUtc ?: "", 
                            email
                        )
                        onSuccess(authResponse)
                    }
                } catch (e: Exception) {
                    onError("Error parsing response: ${e.message}. Response: ${response.toString()}")
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
                headers["Content-Type"] = "application/json"
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * Get available months for submission (matches desktop Worker behavior)
     * Returns months that are at least 1 month in advance OR have user submissions
     * Falls back to unlocked months if the new endpoint is not available
     */
    fun getAvailableMonths(
        onSuccess: (List<MonthDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}months/available"
        
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
                // If 404, fall back to unlocked months and filter client-side
                if (error.networkResponse?.statusCode == 404) {
                    getUnlockedMonthsWithFilter(
                        onSuccess = onSuccess,
                        onError = onError
                    )
                } else {
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
            }
        ) {
            override fun getHeaders(): MutableMap<String, String> {
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
                getAuthHeaders().forEach { (key, value) -> headers[key] = value }
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * Fallback: Get unlocked months and filter client-side to match desktop behavior
     * Shows months that are at least 1 month in advance OR have user submissions
     */
    private fun getUnlockedMonthsWithFilter(
        onSuccess: (List<MonthDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        getUnlockedMonths(
            onSuccess = { unlockedMonths ->
                // Get user's submissions to find months they've submitted to
                getMySubmissions(
                    onSuccess = { submissions ->
                        val submissionMonthKeys = submissions.map { it.monthKey }.toSet()
                        
                        // Calculate next month
                        val now = java.time.LocalDate.now()
                        val currentMonth = java.time.YearMonth.from(now)
                        val nextMonth = currentMonth.plusMonths(1)
                        
                        // Filter: months that are at least next month OR have user submissions
                        val availableMonths = unlockedMonths.filter { month ->
                            try {
                                val parts = month.monthKey.split("-")
                                val monthNum = parts[0].toInt()
                                val year = parts[1].toInt()
                                val monthDate = java.time.YearMonth.of(year, monthNum)
                                
                                // Include if it's at least next month OR user has a submission
                                monthDate >= nextMonth || submissionMonthKeys.contains(month.monthKey)
                            } catch (e: Exception) {
                                false
                            }
                        }.sortedBy { month ->
                            try {
                                val parts = month.monthKey.split("-")
                                val monthNum = parts[0].toInt()
                                val year = parts[1].toInt()
                                java.time.YearMonth.of(year, monthNum)
                            } catch (e: Exception) {
                                java.time.YearMonth.of(9999, 12)
                            }
                        }
                        
                        onSuccess(availableMonths)
                    },
                    onError = { _ ->
                        // If we can't get submissions, just show unlocked months that are at least next month
                        val now = java.time.LocalDate.now()
                        val currentMonth = java.time.YearMonth.from(now)
                        val nextMonth = currentMonth.plusMonths(1)
                        
                        val availableMonths = unlockedMonths.filter { month ->
                            try {
                                val parts = month.monthKey.split("-")
                                val monthNum = parts[0].toInt()
                                val year = parts[1].toInt()
                                val monthDate = java.time.YearMonth.of(year, monthNum)
                                monthDate >= nextMonth
                            } catch (e: Exception) {
                                false
                            }
                        }.sortedBy { month ->
                            try {
                                val parts = month.monthKey.split("-")
                                val monthNum = parts[0].toInt()
                                val year = parts[1].toInt()
                                java.time.YearMonth.of(year, monthNum)
                            } catch (e: Exception) {
                                java.time.YearMonth.of(9999, 12)
                            }
                        }
                        
                        onSuccess(availableMonths)
                    }
                )
            },
            onError = onError
        )
    }
    
    /**
     * Get unlocked months available for submission (kept for backward compatibility)
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
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
        // Get from available months list
        getAvailableMonths(
            onSuccess = { months ->
                val month = months.find { it.monthKey == monthKey }
                if (month != null) {
                    onSuccess(month)
                } else {
                    // Month not in available list, check if it exists and is locked
                    // For now, assume it's locked if not in available list
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
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
        // We use unlocked months here to avoid circular dependency with getAvailableMonths
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
     * Get minimum time range hours setting
     */
    fun getMinTimeRangeHours(
        onSuccess: (Double) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}settings/min-time-range-hours"
        
        val request = AuthenticatedStringRequest(
            Request.Method.GET,
            url,
            getAuthHeaders(),
            { response ->
                try {
                    // Backend returns just a number as string (e.g., "4.0")
                    val hours = response.trim().toDoubleOrNull() ?: 4.0
                    onSuccess(hours)
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
        )
        
        requestQueue.add(request)
    }
    
    /**
     * Get lock after initial submission setting
     */
    fun getLockAfterInitialSubmission(
        onSuccess: (Boolean) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}settings/lock-after-initial-submission"
        
        val request = AuthenticatedStringRequest(
            Request.Method.GET,
            url,
            getAuthHeaders(),
            { response ->
                try {
                    // Backend returns just a boolean as string (e.g., "true" or "false")
                    val enabled = response.trim().toBoolean()
                    onSuccess(enabled)
                } catch (e: Exception) {
                    // Default to false if parsing fails
                    onSuccess(false)
                }
            },
            { error ->
                // Default to false if endpoint doesn't exist or error occurs
                onSuccess(false)
            }
        )
        
        requestQueue.add(request)
    }
    
    /**
     * Get allowed time window setting
     */
    fun getAllowedTimeWindow(
        onSuccess: (String, String) -> Unit,  // startTime, endTime
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}settings/allowed-time-window"
        
        val request = object : JsonObjectRequest(
            Request.Method.GET,
            url,
            null,
            { response ->
                try {
                    val startTime = response.optString("startTime", "07:00")
                    val endTime = response.optString("endTime", "23:00")
                    onSuccess(startTime, endTime)
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
                getAuthHeaders().forEach { (key, value) -> headers[key] = value }
                return headers
            }
        }
        
        requestQueue.add(request)
    }
    
    /**
     * Get notification preference
     */
    fun getNotificationPreference(
        onSuccess: (Boolean) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}settings/notifications"
        
        val request = AuthenticatedStringRequest(
            Request.Method.GET,
            url,
            getAuthHeaders(),
            { response ->
                try {
                    // Backend returns just a boolean as string (e.g., "true" or "false")
                    val enabled = response.trim().toBoolean()
                    onSuccess(enabled)
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
        )
        
        requestQueue.add(request)
    }
    
    /**
     * Update notification preference
     */
    fun updateNotificationPreference(
        enableNotifications: Boolean,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}settings/notifications"
        
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
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
                val superHeaders = super.getHeaders() ?: emptyMap()
                val headers = mutableMapOf<String, String>()
                headers.putAll(superHeaders)
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
