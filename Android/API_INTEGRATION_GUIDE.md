# Android API Integration Guide

This guide explains how to update the Android app's API integration to work with the new ASP.NET Core backend.

## Overview

The backend has been completely rewritten with a new API structure. The main changes are:

1. **JWT Bearer Authentication** - All API calls (except login/register) require a JWT token
2. **New Endpoint Structure** - All endpoints are under `/api/` prefix
3. **New Data Models** - Availability data structure has changed
4. **User-based Submissions** - Submissions are tied to the logged-in user (not worker IDs)

---

## Base URL Configuration

**Current:** `http://10.0.2.2:5180/api/`  
**Check:** Verify the backend port in `AvailabilityCollector/Properties/launchSettings.json`

The base URL should point to where the ASP.NET Core app is running. For Android emulator, use `10.0.2.2` which maps to `localhost` on your development machine.

---

## Authentication (JWT Bearer)

### 1. Update Auth Models

**File:** `app/src/main/java/Android/availibityCollector/data/models/AuthModels.kt`

**Current structure:**
```kotlin
data class AuthResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("message") val message: String?,
    @SerializedName("token") val token: String?,
    @SerializedName("user") val user: UserInfo?
)
```

**New structure:**
```kotlin
data class AuthResponse(
    @SerializedName("token") val token: String,
    @SerializedName("expiresAtUtc") val expiresAtUtc: String  // ISO 8601 format
)

data class LoginRequest(
    @SerializedName("email") val email: String,
    @SerializedName("password") val password: String
)

data class RegisterRequest(
    @SerializedName("email") val email: String,
    @SerializedName("password") val password: String
)
```

### 2. JWT Token Storage

You need to store the JWT token securely (e.g., using `SharedPreferences` or `EncryptedSharedPreferences`). The token should be included in all authenticated API requests.

**Token Storage Example:**
```kotlin
// Create a TokenManager singleton
object TokenManager {
    private const val PREFS_NAME = "auth_prefs"
    private const val KEY_TOKEN = "jwt_token"
    
    fun saveToken(context: Context, token: String) {
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            .edit()
            .putString(KEY_TOKEN, token)
            .apply()
    }
    
    fun getToken(context: Context): String? {
        return context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            .getString(KEY_TOKEN, null)
    }
    
    fun clearToken(context: Context) {
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            .edit()
            .remove(KEY_TOKEN)
            .apply()
    }
}
```

---

## API Endpoints

### Authentication Endpoints

#### POST `/api/auth/register`

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAtUtc": "2026-01-15T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` - Validation errors (password too weak, email already exists, etc.)
- Response body contains array of error objects

#### POST `/api/auth/login`

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAtUtc": "2026-01-15T12:00:00Z"
}
```

**Error Responses:**
- `401 Unauthorized` - Invalid email or password

---

### Months Endpoints

#### GET `/api/months/unlocked`

**Authentication:** Required (JWT Bearer token)

**Response (200 OK):**
```json
[
  {
    "monthKey": "02-2026",
    "isUnlocked": true,
    "lockDateTimeUtc": "2026-02-28T23:59:59Z"
  },
  {
    "monthKey": "03-2026",
    "isUnlocked": true,
    "lockDateTimeUtc": null
  }
]
```

**Note:** Only returns months that are unlocked and not yet locked (deadline hasn't passed).

---

### Availability Endpoints

All availability endpoints require:
- **Authentication:** JWT Bearer token
- **Authorization:** User must have "Worker" role

#### GET `/api/availability/my?monthKey=02-2026`

**Query Parameters:**
- `monthKey` (required) - Format: `MM-yyyy` (e.g., "02-2026")

**Response (200 OK):**
```json
{
  "monthKey": "02-2026",
  "submittedAtUtc": "2026-01-15T10:30:00Z",
  "entries": [
    {
      "date": "2026-02-01",
      "type": "FullDay",
      "startTime": null,
      "endTime": null
    },
    {
      "date": "2026-02-02",
      "type": "TimeRange",
      "startTime": "08:00",
      "endTime": "12:00"
    },
    {
      "date": "2026-02-03",
      "type": "Unavailable",
      "startTime": null,
      "endTime": null
    }
  ]
}
```

**Error Responses:**
- `400 Bad Request` - Missing monthKey parameter
- `401 Unauthorized` - Missing or invalid token
- `403 Forbidden` - User doesn't have Worker role
- `404 Not Found` - Month not found or no submission exists

#### POST `/api/availability/my/submissions`

**Request Body:**
```json
{
  "monthKey": "02-2026",
  "entries": [
    {
      "date": "2026-02-01",
      "type": "FullDay",
      "startTime": null,
      "endTime": null
    },
    {
      "date": "2026-02-02",
      "type": "TimeRange",
      "startTime": "08:00",
      "endTime": "12:00"
    },
    {
      "date": "2026-02-03",
      "type": "Unavailable",
      "startTime": null,
      "endTime": null
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "message": "Submission created successfully",
  "submissionId": 123
}
```

**Error Responses:**
- `400 Bad Request` - Validation errors (see validation rules below)
- `401 Unauthorized` - Missing or invalid token
- `403 Forbidden` - User doesn't have Worker role
- `409 Conflict` - Submission already exists (use PUT to update)

#### PUT `/api/availability/my/submissions/{id}`

**Path Parameters:**
- `id` (required) - Submission ID (integer)

**Request Body:** Same as POST

**Response (200 OK):**
```json
{
  "message": "Submission updated successfully",
  "submissionId": 123
}
```

**Error Responses:**
- `400 Bad Request` - Validation errors or deadline passed
- `401 Unauthorized` - Missing or invalid token
- `403 Forbidden` - User doesn't have Worker role
- `404 Not Found` - Submission not found or doesn't belong to user

#### DELETE `/api/availability/my/submissions/{id}`

**Path Parameters:**
- `id` (required) - Submission ID (integer)

**Response (200 OK):**
```json
{
  "message": "Submission deleted successfully"
}
```

**Error Responses:**
- `400 Bad Request` - Deadline has passed (cannot delete after deadline)
- `401 Unauthorized` - Missing or invalid token
- `403 Forbidden` - User doesn't have Worker role
- `404 Not Found` - Submission not found or doesn't belong to user

---

## Data Models

### New Models Needed

**File:** `app/src/main/java/Android/availibityCollector/data/models/AvailabilityModels.kt`

```kotlin
package Android.availibityCollector.data.models

import com.google.gson.annotations.SerializedName

// Month DTO
data class MonthDto(
    @SerializedName("monthKey") val monthKey: String,  // Format: "MM-yyyy"
    @SerializedName("isUnlocked") val isUnlocked: Boolean,
    @SerializedName("lockDateTimeUtc") val lockDateTimeUtc: String?  // ISO 8601 or null
)

// Availability Entry DTO
enum class AvailabilityType {
    @SerializedName("Unavailable")
    Unavailable,
    
    @SerializedName("FullDay")
    FullDay,
    
    @SerializedName("TimeRange")
    TimeRange
}

data class AvailabilityEntryDto(
    @SerializedName("date") val date: String,  // Format: "yyyy-MM-dd"
    @SerializedName("type") val type: String,  // "Unavailable", "FullDay", or "TimeRange"
    @SerializedName("startTime") val startTime: String?,  // Format: "HH:mm" or null
    @SerializedName("endTime") val endTime: String?  // Format: "HH:mm" or null
)

// Availability Submission DTO (for GET response)
data class AvailabilitySubmissionDto(
    @SerializedName("monthKey") val monthKey: String,
    @SerializedName("submittedAtUtc") val submittedAtUtc: String?,  // ISO 8601 or null
    @SerializedName("entries") val entries: List<AvailabilityEntryDto>
)

// Create/Update Request
data class CreateAvailabilityRequest(
    @SerializedName("monthKey") val monthKey: String,
    @SerializedName("entries") val entries: List<AvailabilityEntryDto>
)
```

---

## Validation Rules

### Date Format
- **Format:** `yyyy-MM-dd` (e.g., "2026-02-15")
- **Required:** Yes

### Type Values
- `"Unavailable"` - User is not available
- `"FullDay"` - User is available for the full day
- `"TimeRange"` - User is available for a specific time range

### Type-Specific Rules

#### Unavailable
- `startTime` must be `null`
- `endTime` must be `null`

#### FullDay
- `startTime` must be `null`
- `endTime` must be `null`

#### TimeRange
- `startTime` is **required** (format: `HH:mm`, e.g., "08:00")
- `endTime` is **required** (format: `HH:mm`, e.g., "17:00")
- `endTime` must be **greater than** `startTime`
- Duration must be **at least 4 hours** (240 minutes)

### Month Validation
- Month must be unlocked (check via `/api/months/unlocked`)
- Submission deadline must not have passed (`lockDateTimeUtc` check)

---

## Updating VolleyClient

### 1. Add JWT Token Support

**File:** `app/src/main/java/Android/availibityCollector/data/api/VolleyClient.kt`

Add token management:

```kotlin
class VolleyClient(context: Context) {
    
    companion object {
        private const val BASE_URL = "http://10.0.2.2:5000/api/"  // Update port if needed
        
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
    private var authToken: String? = null
    
    // Set JWT token for authenticated requests
    fun setAuthToken(token: String?) {
        authToken = token
    }
    
    // Get current token
    fun getAuthToken(): String? = authToken
    
    // Helper method to add Authorization header
    private fun addAuthHeader(headers: MutableMap<String, String>): MutableMap<String, String> {
        authToken?.let {
            headers["Authorization"] = "Bearer $it"
        }
        return headers
    }
    
    // ... rest of methods
}
```

### 2. Update Login Method

```kotlin
fun login(
    email: String,
    password: String,
    onSuccess: (AuthResponse) -> Unit,
    onError: (String) -> Unit
) {
    val url = "${BASE_URL}auth/login"
    
    val requestBody = JSONObject().apply {
        put("email", email)
        put("password", password)
    }
    
    val request = JsonObjectRequest(
        Request.Method.POST,
        url,
        requestBody,
        { response ->
            try {
                val authResponse: AuthResponse = gson.fromJson(response.toString(), AuthResponse::class.java)
                // Store token
                setAuthToken(authResponse.token)
                // Also save to TokenManager for persistence
                TokenManager.saveToken(context, authResponse.token)
                onSuccess(authResponse)
            } catch (e: Exception) {
                onError("Error parsing response: ${e.message}")
            }
        },
        { error ->
            val errorMessage = when {
                error.networkResponse != null -> {
                    val statusCode = error.networkResponse.statusCode
                    val errorBody = String(error.networkResponse.data ?: ByteArray(0))
                    "HTTP $statusCode: $errorBody"
                }
                error.message != null -> error.message!!
                else -> "Unknown network error"
            }
            onError(errorMessage)
        }
    )
    
    requestQueue.add(request)
}
```

### 3. Update Register Method

Similar to login, but use `/api/auth/register` endpoint.

### 4. Add Get Unlocked Months Method

```kotlin
fun getUnlockedMonths(
    onSuccess: (List<MonthDto>) -> Unit,
    onError: (String) -> Unit
) {
    val url = "${BASE_URL}months/unlocked"
    
    val request = JsonArrayRequest(
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
            handleError(error, onError)
        }
    ) {
        override fun getHeaders(): MutableMap<String, String> {
            return addAuthHeader(super.getHeaders() ?: mutableMapOf())
        }
    }
    
    requestQueue.add(request)
}
```

### 5. Add Get My Availability Method

```kotlin
fun getMyAvailability(
    monthKey: String,
    onSuccess: (AvailabilitySubmissionDto) -> Unit,
    onError: (String) -> Unit
) {
    val url = "${BASE_URL}availability/my?monthKey=${monthKey}"
    
    val request = JsonObjectRequest(
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
            handleError(error, onError)
        }
    ) {
        override fun getHeaders(): MutableMap<String, String> {
            return addAuthHeader(super.getHeaders() ?: mutableMapOf())
        }
    }
    
    requestQueue.add(request)
}
```

### 6. Add Create Submission Method

```kotlin
fun createAvailabilitySubmission(
    request: CreateAvailabilityRequest,
    onSuccess: (Int) -> Unit,  // Returns submissionId
    onError: (String) -> Unit
) {
    val url = "${BASE_URL}availability/my/submissions"
    
    val requestBody = gson.toJsonTree(request).asJsonObject
    
    val jsonRequest = object : JsonObjectRequest(
        Request.Method.POST,
        url,
        requestBody,
        { response ->
            try {
                val submissionId = response.getInt("submissionId")
                onSuccess(submissionId)
            } catch (e: Exception) {
                onError("Error parsing response: ${e.message}")
            }
        },
        { error ->
            handleError(error, onError)
        }
    ) {
        override fun getHeaders(): MutableMap<String, String> {
            return addAuthHeader(super.getHeaders() ?: mutableMapOf())
        }
    }
    
    requestQueue.add(jsonRequest)
}
```

### 7. Add Update Submission Method

```kotlin
fun updateAvailabilitySubmission(
    submissionId: Int,
    request: CreateAvailabilityRequest,
    onSuccess: () -> Unit,
    onError: (String) -> Unit
) {
    val url = "${BASE_URL}availability/my/submissions/$submissionId"
    
    val requestBody = gson.toJsonTree(request).asJsonObject
    
    val jsonRequest = object : JsonObjectRequest(
        Request.Method.PUT,
        url,
        requestBody,
        { _ ->
            onSuccess()
        },
        { error ->
            handleError(error, onError)
        }
    ) {
        override fun getHeaders(): MutableMap<String, String> {
            return addAuthHeader(super.getHeaders() ?: mutableMapOf())
        }
    }
    
    requestQueue.add(jsonRequest)
}
```

### 8. Add Delete Submission Method

```kotlin
fun deleteAvailabilitySubmission(
    submissionId: Int,
    onSuccess: () -> Unit,
    onError: (String) -> Unit
) {
    val url = "${BASE_URL}availability/my/submissions/$submissionId"
    
    val request = object : JsonObjectRequest(
        Request.Method.DELETE,
        url,
        null,
        { _ ->
            onSuccess()
        },
        { error ->
            handleError(error, onError)
        }
    ) {
        override fun getHeaders(): MutableMap<String, String> {
            return addAuthHeader(super.getHeaders() ?: mutableMapOf())
        }
    }
    
    requestQueue.add(request)
}
```

### 9. Helper Method for Error Handling

```kotlin
private fun handleError(error: VolleyError, onError: (String) -> Unit) {
    val errorMessage = when {
        error.networkResponse != null -> {
            val statusCode = error.networkResponse.statusCode
            val errorBody = String(error.networkResponse.data ?: ByteArray(0))
            try {
                val errorJson = JSONObject(errorBody)
                errorJson.optString("error", "HTTP $statusCode: $errorBody")
            } catch (e: Exception) {
                "HTTP $statusCode: $errorBody"
            }
        }
        error.message != null -> error.message!!
        else -> "Unknown network error"
    }
    onError(errorMessage)
}
```

---

## Remove Old Methods

Remove these old methods from `VolleyClient.kt`:
- `getWorkers()` - No longer exists
- `createRazpolozljivost()` - Replaced by `createAvailabilitySubmission()`
- `getRazpolozljivostByWorker()` - Replaced by `getMyAvailability()`

---

## Update Screens

### LoginScreen.kt

Update to use the new login method and store the JWT token:

```kotlin
// In LoginScreen composable
volleyClient.login(
    email = email,
    password = password,
    onSuccess = { authResponse ->
        // Token is automatically stored in VolleyClient and TokenManager
        onLoginSuccess(email)
    },
    onError = { error ->
        errorMessage = error
        isLoading = false
    }
)
```

### AvailabilityScreen.kt

1. **Load unlocked months first:**
```kotlin
LaunchedEffect(Unit) {
    volleyClient.getUnlockedMonths(
        onSuccess = { months ->
            // Filter months for current month
            val currentMonthKey = currentMonth.format(DateTimeFormatter.ofPattern("MM-yyyy"))
            val isUnlocked = months.any { it.monthKey == currentMonthKey }
            if (!isUnlocked) {
                // Show message that month is locked
            }
        },
        onError = { error ->
            // Handle error
        }
    )
}
```

2. **Load existing submission:**
```kotlin
LaunchedEffect(currentMonth) {
    val monthKey = currentMonth.format(DateTimeFormatter.ofPattern("MM-yyyy"))
    volleyClient.getMyAvailability(
        monthKey = monthKey,
        onSuccess = { submission ->
            // Populate calendar with existing entries
            submission.entries.forEach { entry ->
                val date = LocalDate.parse(entry.date)
                // Map entry.type to availability status
                // Map entry.startTime/endTime to time slots
            }
        },
        onError = { error ->
            // If 404, no submission exists yet (this is OK)
            // Other errors should be shown to user
        }
    )
}
```

3. **Save submission:**
```kotlin
// Convert calendar selections to CreateAvailabilityRequest
val entries = availability.map { (date, status) ->
    when (status) {
        1 -> AvailabilityEntryDto(  // FullDay
            date = date.format(DateTimeFormatter.ofPattern("yyyy-MM-dd")),
            type = "FullDay",
            startTime = null,
            endTime = null
        )
        2 -> AvailabilityEntryDto(  // Unavailable
            date = date.format(DateTimeFormatter.ofPattern("yyyy-MM-dd")),
            type = "Unavailable",
            startTime = null,
            endTime = null
        )
        3 -> {  // TimeRange
            val timeSlot = timeSlots[date]?.firstOrNull()
            val (start, end) = timeSlot?.split(" - ") ?: ("", "")
            AvailabilityEntryDto(
                date = date.format(DateTimeFormatter.ofPattern("yyyy-MM-dd")),
                type = "TimeRange",
                startTime = start,
                endTime = end
            )
        }
        else -> null
    }
}.filterNotNull()

val request = CreateAvailabilityRequest(
    monthKey = currentMonth.format(DateTimeFormatter.ofPattern("MM-yyyy")),
    entries = entries
)

volleyClient.createAvailabilitySubmission(
    request = request,
    onSuccess = { submissionId ->
        // Show success message
    },
    onError = { error ->
        // Show error message
    }
)
```

---

## Testing Checklist

- [ ] Login with valid credentials returns JWT token
- [ ] Login with invalid credentials returns 401
- [ ] Register creates new user and returns JWT token
- [ ] Get unlocked months returns list of unlocked months
- [ ] Get my availability returns submission for a month
- [ ] Get my availability returns 404 if no submission exists
- [ ] Create submission successfully creates new submission
- [ ] Create submission returns 409 if submission already exists
- [ ] Update submission successfully updates existing submission
- [ ] Delete submission successfully deletes submission
- [ ] All authenticated requests include JWT token in Authorization header
- [ ] Requests without token return 401
- [ ] Validation errors are properly displayed to user

---

## Notes

1. **Token Expiration:** JWT tokens expire after 12 hours. You may want to implement token refresh logic or re-login when token expires.

2. **Error Handling:** Always check HTTP status codes and parse error messages from the response body.

3. **Date/Time Formats:**
   - Dates: `yyyy-MM-dd` (e.g., "2026-02-15")
   - Times: `HH:mm` (e.g., "08:00", "17:30")
   - UTC timestamps: ISO 8601 format (e.g., "2026-02-15T10:30:00Z")

4. **Month Key Format:** Always use `MM-yyyy` format (e.g., "02-2026" for February 2026).

5. **User Context:** The backend automatically identifies the user from the JWT token, so you don't need to pass user IDs or worker IDs.

---

## Questions?

If you encounter any issues or need clarification, check:
- Backend API documentation at `/swagger` (when backend is running)
- Backend controller code in `AvailabilityCollector/Controllers/Api/`
- Backend models in `AvailabilityCollector/Models/`
