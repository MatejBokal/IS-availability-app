package Android.availibityCollector.data.repository

import Android.availibityCollector.data.api.RetrofitClient
import Android.availibityCollector.data.models.Razpolozljivost
import Android.availibityCollector.data.models.RazpolozljivostCreate

class RazpolozljivostRepository {
    
    private val apiService = RetrofitClient.apiService
    
    suspend fun getRazpolozljivosti(): Result<List<Razpolozljivost>> {
        return try {
            val response = apiService.getRazpolozljivosti()
            if (response.isSuccessful && response.body() != null) {
                Result.Success(response.body()!!)
            } else {
                Result.Error(response.message() ?: "Napaka pri pridobivanju razpoložljivosti")
            }
        } catch (e: Exception) {
            Result.Success(emptyList())
        }
    }
    
    suspend fun getRazpolozljivostiByWorker(workerId: Int): Result<List<Razpolozljivost>> {
        return try {
            val response = apiService.getRazpolozljivostiByWorker(workerId)
            if (response.isSuccessful && response.body() != null) {
                Result.Success(response.body()!!)
            } else {
                Result.Error(response.message() ?: "Napaka")
            }
        } catch (e: Exception) {
            Result.Success(emptyList())
        }
    }
    
    suspend fun createRazpolozljivost(razpolozljivost: RazpolozljivostCreate): Result<Razpolozljivost> {
        return try {
            val response = apiService.createRazpolozljivost(razpolozljivost)
            if (response.isSuccessful && response.body() != null) {
                Result.Success(response.body()!!)
            } else {
                Result.Error(response.message() ?: "Napaka pri shranjevanju")
            }
        } catch (e: Exception) {
            Result.Error(e.message ?: "Napaka pri povezavi")
        }
    }
    
    suspend fun updateRazpolozljivost(id: Int, razpolozljivost: Razpolozljivost): Result<Razpolozljivost> {
        return try {
            val response = apiService.updateRazpolozljivost(id, razpolozljivost)
            if (response.isSuccessful && response.body() != null) {
                Result.Success(response.body()!!)
            } else {
                Result.Error(response.message() ?: "Napaka pri posodabljanju")
            }
        } catch (e: Exception) {
            Result.Error(e.message ?: "Napaka pri povezavi")
        }
    }
    
    suspend fun deleteRazpolozljivost(id: Int): Result<Unit> {
        return try {
            val response = apiService.deleteRazpolozljivost(id)
            if (response.isSuccessful) {
                Result.Success(Unit)
            } else {
                Result.Error(response.message() ?: "Napaka pri brisanju")
            }
        } catch (e: Exception) {
            Result.Error(e.message ?: "Napaka pri povezavi")
        }
    }
}
