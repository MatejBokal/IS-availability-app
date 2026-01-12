package Android.availibityCollector.data.repository

import Android.availibityCollector.data.api.RetrofitClient
import Android.availibityCollector.data.models.Worker

class WorkerRepository {
    
    private val apiService = RetrofitClient.apiService
    
    suspend fun getWorkers(): Result<List<Worker>> {
        return try {
            val response = apiService.getWorkers()
            if (response.isSuccessful && response.body() != null) {
                Result.Success(response.body()!!)
            } else {
                Result.Error(response.message() ?: "Napaka pri pridobivanju delavcev")
            }
        } catch (e: Exception) {
            // Return sample data for demo
            Result.Success(getSampleWorkers())
        }
    }
    
    suspend fun getWorker(id: Int): Result<Worker> {
        return try {
            val response = apiService.getWorker(id)
            if (response.isSuccessful && response.body() != null) {
                Result.Success(response.body()!!)
            } else {
                Result.Error(response.message() ?: "Delavec ni bil najden")
            }
        } catch (e: Exception) {
            Result.Error(e.message ?: "Napaka pri povezavi")
        }
    }
    
    private fun getSampleWorkers(): List<Worker> {
        return listOf(
            Worker(1, "Janez", "Novak", "Prodajalec", "Študent", "2025-01-01", true),
            Worker(2, "Maja", "Horvat", "Blagajnik", "Študent", "2025-01-01", true),
            Worker(3, "Peter", "Krajnc", "Skladiščnik", "Redno zaposleni", "2025-01-01", true),
            Worker(4, "Ana", "Zupan", "Prodajalec", "Študent", "2025-01-01", false)
        )
    }
}
