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
import Android.availibityCollector.data.models.Worker
import Android.availibityCollector.data.models.Razpolozljivost

/**
 * Volley client for REST API communication
 * Implements:
 * - READ operation: Get all workers
 * - CREATE operation: Create new availability (Razpolozljivost)
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
    
    /**
     * READ operation - Get all workers from the REST API
     * Workers can view the list of all colleagues
     */
    fun getWorkers(
        onSuccess: (List<Worker>) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}workersapi"
        
        val request = JsonArrayRequest(
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
        )
        
        requestQueue.add(request)
    }
    
    /**
     * CREATE operation - Create a new availability entry
     * Workers submit their own availability for scheduling
     */
    fun createRazpolozljivost(
        workerId: Int,
        razpolozljivostJSON: String,
        mesecLeto: String,
        type: String,
        zaporedniTeden: Int?,
        onSuccess: (Razpolozljivost) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}razpolozljivostiapi"
        
        val jsonBody = JSONObject().apply {
            put("workerId", workerId)
            put("razpolozljivostJSON", razpolozljivostJSON)
            put("mesecLeto", mesecLeto)
            put("type", type)
            if (zaporedniTeden != null) {
                put("zaporedniTeden", zaporedniTeden)
            }
        }
        
        val request = JsonObjectRequest(
            Request.Method.POST,
            url,
            jsonBody,
            { response ->
                try {
                    val razpolozljivost: Razpolozljivost = gson.fromJson(response.toString(), Razpolozljivost::class.java)
                    onSuccess(razpolozljivost)
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
        )
        
        requestQueue.add(request)
    }
    
    /**
     * READ operation - Get availability for a specific worker
     * Loads saved availability when opening the screen
     */
    fun getRazpolozljivostByWorker(
        workerId: Int,
        onSuccess: (List<Razpolozljivost>) -> Unit,
        onError: (String) -> Unit
    ) {
        val url = "${BASE_URL}razpolozljivostiapi/worker/$workerId"
        
        val request = JsonArrayRequest(
            Request.Method.GET,
            url,
            null,
            { response ->
                try {
                    val type = object : TypeToken<List<Razpolozljivost>>() {}.type
                    val razpolozljivosti: List<Razpolozljivost> = gson.fromJson(response.toString(), type)
                    onSuccess(razpolozljivosti)
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
        )
        
        requestQueue.add(request)
    }
    
    /**
     * Cancel all pending requests
     */
    fun cancelAllRequests() {
        requestQueue.cancelAll { true }
    }
}
