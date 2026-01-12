package Android.availibityCollector.data.api

import Android.availibityCollector.data.models.*
import retrofit2.Response
import retrofit2.http.*

interface ApiService {
    
    // Authentication
    @POST("api/auth/login")
    suspend fun login(@Body request: LoginRequest): Response<AuthResponse>
    
    @POST("api/auth/register")
    suspend fun register(@Body request: RegisterRequest): Response<AuthResponse>
    
    @POST("api/auth/logout")
    suspend fun logout(): Response<Unit>
    
    // Workers
    @GET("api/workers")
    suspend fun getWorkers(): Response<List<Worker>>
    
    @GET("api/workers/{id}")
    suspend fun getWorker(@Path("id") id: Int): Response<Worker>
    
    @POST("api/workers")
    suspend fun createWorker(@Body worker: Worker): Response<Worker>
    
    @PUT("api/workers/{id}")
    suspend fun updateWorker(@Path("id") id: Int, @Body worker: Worker): Response<Worker>
    
    @DELETE("api/workers/{id}")
    suspend fun deleteWorker(@Path("id") id: Int): Response<Unit>
    
    // Razpolozljivosti
    @GET("api/razpolozljivosti")
    suspend fun getRazpolozljivosti(): Response<List<Razpolozljivost>>
    
    @GET("api/razpolozljivosti/{id}")
    suspend fun getRazpolozljivost(@Path("id") id: Int): Response<Razpolozljivost>
    
    @GET("api/razpolozljivosti/worker/{workerId}")
    suspend fun getRazpolozljivostiByWorker(@Path("workerId") workerId: Int): Response<List<Razpolozljivost>>
    
    @POST("api/razpolozljivosti")
    suspend fun createRazpolozljivost(@Body razpolozljivost: RazpolozljivostCreate): Response<Razpolozljivost>
    
    @PUT("api/razpolozljivosti/{id}")
    suspend fun updateRazpolozljivost(@Path("id") id: Int, @Body razpolozljivost: Razpolozljivost): Response<Razpolozljivost>
    
    @DELETE("api/razpolozljivosti/{id}")
    suspend fun deleteRazpolozljivost(@Path("id") id: Int): Response<Unit>
}
