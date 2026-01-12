package Android.availibityCollector.data.models

import com.google.gson.annotations.SerializedName

data class Razpolozljivost(
    @SerializedName("id") val id: Int,
    @SerializedName("razpolozljivostJSON") val razpolozljivostJSON: String,
    @SerializedName("mesecLeto") val mesecLeto: String,
    @SerializedName("type") val type: String,
    @SerializedName("zaporedniTeden") val zaporedniTeden: Int?,
    @SerializedName("workerID") val workerId: Int
)

data class RazpolozljivostCreate(
    @SerializedName("razpolozljivostJSON") val razpolozljivostJSON: String,
    @SerializedName("mesecLeto") val mesecLeto: String,
    @SerializedName("type") val type: String,
    @SerializedName("zaporedniTeden") val zaporedniTeden: Int?,
    @SerializedName("workerID") val workerId: Int
)
