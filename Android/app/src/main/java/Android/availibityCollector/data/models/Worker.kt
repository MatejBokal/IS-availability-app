package Android.availibityCollector.data.models

import com.google.gson.annotations.SerializedName

data class Worker(
    @SerializedName("id") val id: Int,
    @SerializedName("ime") val ime: String,
    @SerializedName("priimek") val priimek: String,
    @SerializedName("delovnoMesto") val delovnoMesto: String,
    @SerializedName("vrstaZaposlitve") val vrstaZaposlitve: String,
    @SerializedName("createdAt") val createdAt: String,
    @SerializedName("isActive") val isActive: Boolean
)
