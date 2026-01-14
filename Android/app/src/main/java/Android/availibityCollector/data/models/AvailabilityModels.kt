package Android.availibityCollector.data.models

import com.google.gson.annotations.SerializedName

enum class AvailabilityType {
    @SerializedName("Unavailable")
    Unavailable,
    @SerializedName("FullDay")
    FullDay,
    @SerializedName("TimeRange")
    TimeRange
}

data class AvailabilityEntryDto(
    @SerializedName("date") val date: String,  // Format: yyyy-MM-dd
    @SerializedName("type") val type: String,  // "Unavailable", "FullDay", "TimeRange"
    @SerializedName("startTime") val startTime: String? = null,  // Format: HH:mm
    @SerializedName("endTime") val endTime: String? = null  // Format: HH:mm
)

data class CreateAvailabilityRequest(
    @SerializedName("monthKey") val monthKey: String,  // Format: MM-yyyy
    @SerializedName("entries") val entries: List<AvailabilityEntryDto>
)

data class AvailabilitySubmissionDto(
    @SerializedName("monthKey") val monthKey: String,
    @SerializedName("submittedAtUtc") val submittedAtUtc: String?,
    @SerializedName("entries") val entries: List<AvailabilityEntryDto>
)

data class MonthDto(
    @SerializedName("monthKey") val monthKey: String,  // Format: MM-yyyy
    @SerializedName("isUnlocked") val isUnlocked: Boolean,
    @SerializedName("lockDateTimeUtc") val lockDateTimeUtc: String?  // ISO 8601 format
)
