package Android.availibityCollector.screens

import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import Android.availibityCollector.data.api.VolleyClient
import Android.availibityCollector.data.models.AvailabilityModels.*
import java.time.LocalDate
import java.time.YearMonth
import java.time.format.DateTimeFormatter
import java.util.*

/**
 * AvailabilityScreen - Submit/Update availability for a specific month
 * Uses new API structure with AvailabilityType enum
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AvailabilityScreen(
    monthKey: String,  // Format: MM-yyyy
    onBackClick: () -> Unit,
    readOnly: Boolean = false  // For history view
) {
    val context = LocalContext.current
    val volleyClient = remember { VolleyClient.getInstance(context) }
    
    // Parse monthKey
    val (yearMonth, monthName) = remember(monthKey) {
        try {
            val parts = monthKey.split("-")
            val monthNum = parts[0].toInt()
            val year = parts[1].toInt()
            val ym = YearMonth.of(year, monthNum)
            val name = ym.format(DateTimeFormatter.ofPattern("MMMM yyyy", Locale("sl")))
            Pair(ym, name)
        } catch (e: Exception) {
            Pair(YearMonth.now(), monthKey)
        }
    }
    
    var selectedDates by remember { mutableStateOf(setOf<LocalDate>()) }
    var isSaving by remember { mutableStateOf(false) }
    var isLoading by remember { mutableStateOf(true) }
    var isLocked by remember { mutableStateOf(false) }
    var submissionId by remember { mutableStateOf<Int?>(null) }
    var minDurationMinutes by remember { mutableStateOf(240) } // Default 4 hours
    
    // Track availability: LocalDate -> AvailabilityEntryDto
    val availability = remember { mutableStateMapOf<LocalDate, AvailabilityEntryDto>() }
    
    // State for custom time input
    var customStartTime by remember { mutableStateOf("") }
    var customEndTime by remember { mutableStateOf("") }
    var showTimeInput by remember { mutableStateOf(false) }
    
    // Load month info and existing submission
    LaunchedEffect(monthKey) {
        // Helper function to load submission (defined inside LaunchedEffect scope)
        fun loadSubmission() {
            volleyClient.getMyAvailability(
                monthKey = monthKey,
                onSuccess = { submission ->
                    submissionId = null // We'll need to track this differently
                    submission.entries.forEach { entry ->
                        val date = LocalDate.parse(entry.date)
                        availability[date] = entry
                    }
                    isLoading = false
                },
                onError = { error ->
                    // 404 means no submission yet, which is fine
                    if (error.contains("404") || error.contains("No submission")) {
                        isLoading = false
                    } else {
                        Toast.makeText(context, "Napaka: $error", Toast.LENGTH_LONG).show()
                        isLoading = false
                    }
                }
            )
        }
        
        // First check if month is locked (unless read-only mode)
        if (!readOnly) {
            volleyClient.getMonthInfo(
                monthKey = monthKey,
                onSuccess = { month ->
                    // Check if month is locked
                    val now = java.time.Instant.now()
                    val locked = !month.isUnlocked || 
                        (month.lockDateTimeUtc != null && try {
                            val lockDate = java.time.Instant.parse(month.lockDateTimeUtc)
                            now.isAfter(lockDate)
                        } catch (e: Exception) {
                            false
                        })
                    isLocked = locked
                    
                    // Then load submission
                    loadSubmission()
                },
                onError = { _ ->
                    // If month not found in unlocked list, assume locked
                    isLocked = true
                    loadSubmission()
                }
            )
        } else {
            // Read-only mode - just load submission
            isLocked = true // Always locked in read-only mode
            loadSubmission()
        }
    }
    
    // Helper to apply availability type to selected dates
    fun applyAvailabilityType(type: AvailabilityType, startTime: String? = null, endTime: String? = null) {
        selectedDates.forEach { date ->
            availability[date] = AvailabilityEntryDto(
                date = date.format(DateTimeFormatter.ISO_LOCAL_DATE),
                type = type.name,
                startTime = startTime,
                endTime = endTime
            )
        }
        selectedDates = emptySet()
        showTimeInput = false
        customStartTime = ""
        customEndTime = ""
    }
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Razpoložljivost - $monthName") },
                navigationIcon = {
                    IconButton(onClick = onBackClick) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Nazaj"
                        )
                    }
                }
            )
        }
    ) { paddingValues ->
        if (isLoading) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues),
                contentAlignment = Alignment.Center
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(16.dp)
                ) {
                    CircularProgressIndicator()
                    Text("Nalagam razpoložljivost...")
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(16.dp)
            ) {
                if (isLocked || readOnly) {
                    item {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = if (readOnly) 
                                    MaterialTheme.colorScheme.primaryContainer 
                                else 
                                    MaterialTheme.colorScheme.errorContainer
                            )
                        ) {
                            Text(
                                text = if (readOnly) 
                                    "Samo pregled oddane razpoložljivosti. Urejanje ni mogoče."
                                else 
                                    "Ta mesec je zaklenjen in ga ni mogoče urejati. Samo pregled je na voljo.",
                                modifier = Modifier.padding(16.dp),
                                fontSize = 14.sp,
                                color = if (readOnly)
                                    MaterialTheme.colorScheme.onPrimaryContainer
                                else
                                    MaterialTheme.colorScheme.onErrorContainer
                            )
                        }
                        Spacer(modifier = Modifier.height(16.dp))
                    }
                }
                
                // Calendar grid
                item {
                    CalendarGrid(
                        yearMonth = yearMonth,
                        selectedDates = selectedDates,
                        availability = availability,
                        onDateClick = { date ->
                            if (!isLocked && !readOnly) {
                                selectedDates = if (selectedDates.contains(date)) {
                                    selectedDates - date
                                } else {
                                    selectedDates + date
                                }
                            }
                        }
                    )
                    Spacer(modifier = Modifier.height(16.dp))
                }
                
                // Selection controls
                item {
                    if (selectedDates.isNotEmpty() && !isLocked && !readOnly) {
                        SelectionControls(
                            selectedCount = selectedDates.size,
                            selectedDates = selectedDates,
                            onClearSelection = { selectedDates = emptySet() },
                            onSetFullDay = { applyAvailabilityType(AvailabilityType.FullDay) },
                            onSetUnavailable = { applyAvailabilityType(AvailabilityType.Unavailable) },
                            onSetTimeRange = { showTimeInput = true },
                            showTimeInput = showTimeInput,
                            startTime = customStartTime,
                            endTime = customEndTime,
                            onStartTimeChange = { customStartTime = it },
                            onEndTimeChange = { customEndTime = it },
                            onConfirmTimeRange = {
                                if (customStartTime.isNotBlank() && customEndTime.isNotBlank()) {
                                    // Validate minimum duration
                                    try {
                                        val startParts = customStartTime.split(":")
                                        val endParts = customEndTime.split(":")
                                        val startMinutes = startParts[0].toInt() * 60 + startParts[1].toInt()
                                        val endMinutes = endParts[0].toInt() * 60 + endParts[1].toInt()
                                        val duration = endMinutes - startMinutes
                                        
                                        if (duration < minDurationMinutes) {
                                            Toast.makeText(
                                                context,
                                                "Časovni razpon mora biti vsaj ${minDurationMinutes / 60} ur",
                                                Toast.LENGTH_LONG
                                            ).show()
                                            return@SelectionControls
                                        }
                                        
                                        if (endMinutes <= startMinutes) {
                                            Toast.makeText(
                                                context,
                                                "Končni čas mora biti za začetnim časom",
                                                Toast.LENGTH_LONG
                                            ).show()
                                            return@SelectionControls
                                        }
                                        
                                        applyAvailabilityType(
                                            AvailabilityType.TimeRange,
                                            customStartTime,
                                            customEndTime
                                        )
                                    } catch (e: Exception) {
                                        Toast.makeText(
                                            context,
                                            "Neveljaven format časa. Uporabite HH:mm",
                                            Toast.LENGTH_LONG
                                        ).show()
                                    }
                                }
                            }
                        )
                        Spacer(modifier = Modifier.height(16.dp))
                    }
                }
                
                // Legend
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.surfaceVariant
                        )
                    ) {
                        Column(modifier = Modifier.padding(12.dp)) {
                            Text(
                                text = "Legenda:",
                                fontWeight = FontWeight.SemiBold,
                                fontSize = 14.sp
                            )
                            Spacer(modifier = Modifier.height(8.dp))
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceEvenly
                            ) {
                                LegendItem(color = Color(0xFF4CAF50), text = "Na voljo")
                                LegendItem(color = Color(0xFFFF9800), text = "Delno")
                                LegendItem(color = Color(0xFFF44336), text = "Ni na voljo")
                            }
                        }
                    }
                    Spacer(modifier = Modifier.height(16.dp))
                }
                
                // Submit button
                if (!isLocked && !readOnly) {
                    item {
                        Button(
                            onClick = {
                                if (availability.isEmpty()) {
                                    Toast.makeText(
                                        context,
                                        "Označite vsaj en dan",
                                        Toast.LENGTH_SHORT
                                    ).show()
                                    return@Button
                                }
                                
                                isSaving = true
                                
                                val entries = availability.values.toList()
                                val request = CreateAvailabilityRequest(
                                    monthKey = monthKey,
                                    entries = entries
                                )
                                
                                val onSuccess: () -> Unit = {
                                    isSaving = false
                                    Toast.makeText(
                                        context,
                                        "Razpoložljivost je bila uspešno shranjena!",
                                        Toast.LENGTH_SHORT
                                    ).show()
                                    onBackClick()
                                }
                                
                                val onError: (String) -> Unit = { error ->
                                    isSaving = false
                                    Toast.makeText(
                                        context,
                                        "Napaka: $error",
                                        Toast.LENGTH_LONG
                                    ).show()
                                }
                                
                                if (submissionId != null) {
                                    volleyClient.updateAvailabilitySubmission(
                                        submissionId = submissionId!!,
                                        request = request,
                                        onSuccess = onSuccess,
                                        onError = onError
                                    )
                                } else {
                                    volleyClient.createAvailabilitySubmission(
                                        request = request,
                                        onSuccess = { id ->
                                            submissionId = id
                                            onSuccess()
                                        },
                                        onError = onError
                                    )
                                }
                            },
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(50.dp),
                            enabled = !isSaving && availability.isNotEmpty()
                        ) {
                            if (isSaving) {
                                CircularProgressIndicator(
                                    modifier = Modifier.size(24.dp),
                                    color = MaterialTheme.colorScheme.onPrimary
                                )
                            } else {
                                Text(
                                    text = "Shrani razpoložljivost",
                                    fontSize = 16.sp,
                                    fontWeight = FontWeight.Medium
                                )
                            }
                        }
                        
                        Spacer(modifier = Modifier.height(32.dp))
                    }
                }
            }
        }
    }
}

@Composable
private fun CalendarGrid(
    yearMonth: YearMonth,
    selectedDates: Set<LocalDate>,
    availability: Map<LocalDate, AvailabilityEntryDto>,
    onDateClick: (LocalDate) -> Unit
) {
    val firstDayOfMonth = yearMonth.atDay(1)
    val lastDayOfMonth = yearMonth.atEndOfMonth()
    val firstDayOfWeek = (firstDayOfMonth.dayOfWeek.value - 1) % 7 // Monday = 0
    val daysInMonth = yearMonth.lengthOfMonth()
    
    val totalCells = firstDayOfWeek + daysInMonth
    val rows = (totalCells + 6) / 7
    
    Card {
        Column(modifier = Modifier.padding(8.dp)) {
            // Day headers
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                listOf("Po", "To", "Sr", "Če", "Pe", "So", "Ne").forEach { day ->
                    Text(
                        text = day,
                        modifier = Modifier.weight(1f),
                        textAlign = TextAlign.Center,
                        fontWeight = FontWeight.SemiBold,
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
            
            Spacer(modifier = Modifier.height(8.dp))
            
            // Calendar cells
            Column {
                for (week in 0 until rows) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceEvenly
                    ) {
                        for (dayOfWeek in 0..6) {
                            val cellIndex = week * 7 + dayOfWeek
                            val dayOfMonth = cellIndex - firstDayOfWeek + 1
                            
                            if (dayOfMonth in 1..daysInMonth) {
                                val date = yearMonth.atDay(dayOfMonth)
                                val isSelected = selectedDates.contains(date)
                                val entry = availability[date]
                                
                                val backgroundColor = when (entry?.type) {
                                    "FullDay" -> Color(0xFF4CAF50).copy(alpha = 0.3f)
                                    "TimeRange" -> Color(0xFFFF9800).copy(alpha = 0.3f)
                                    "Unavailable" -> Color(0xFFF44336).copy(alpha = 0.3f)
                                    else -> Color.Transparent
                                }
                                
                                Box(
                                    modifier = Modifier
                                        .weight(1f)
                                        .aspectRatio(1f)
                                        .padding(2.dp)
                                        .background(
                                            color = backgroundColor,
                                            shape = MaterialTheme.shapes.small
                                        )
                                        .border(
                                            width = if (isSelected) 2.dp else 0.dp,
                                            color = if (isSelected) MaterialTheme.colorScheme.primary else Color.Transparent,
                                            shape = MaterialTheme.shapes.small
                                        )
                                        .clickable { onDateClick(date) },
                                    contentAlignment = Alignment.Center
                                ) {
                                    Column(
                                        horizontalAlignment = Alignment.CenterHorizontally,
                                        verticalArrangement = Arrangement.Center
                                    ) {
                                        Text(
                                            text = dayOfMonth.toString(),
                                            fontSize = 14.sp,
                                            fontWeight = if (date == LocalDate.now()) FontWeight.Bold else FontWeight.Normal,
                                            color = if (date == LocalDate.now()) 
                                                MaterialTheme.colorScheme.primary 
                                            else 
                                                MaterialTheme.colorScheme.onSurface
                                        )
                                        if (entry?.type == "TimeRange" && entry.startTime != null && entry.endTime != null) {
                                            Text(
                                                text = "${entry.startTime}-${entry.endTime}",
                                                fontSize = 8.sp,
                                                color = MaterialTheme.colorScheme.onSurface
                                            )
                                        }
                                    }
                                }
                            } else {
                                Spacer(modifier = Modifier.weight(1f))
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun SelectionControls(
    selectedCount: Int,
    selectedDates: Set<LocalDate>,
    onClearSelection: () -> Unit,
    onSetFullDay: () -> Unit,
    onSetUnavailable: () -> Unit,
    onSetTimeRange: () -> Unit,
    showTimeInput: Boolean,
    startTime: String,
    endTime: String,
    onStartTimeChange: (String) -> Unit,
    onEndTimeChange: (String) -> Unit,
    onConfirmTimeRange: () -> Unit
) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Izbrano: $selectedCount dni",
                    fontSize = 14.sp,
                    color = MaterialTheme.colorScheme.primary,
                    fontWeight = FontWeight.Medium
                )
                TextButton(onClick = onClearSelection) {
                    Text("Počisti izbor")
                }
            }
            
            Spacer(modifier = Modifier.height(12.dp))
            
            Text(
                text = "Označi razpoložljivost za izbrane dni:",
                fontSize = 14.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            
            Spacer(modifier = Modifier.height(8.dp))
            
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(
                    onClick = onSetFullDay,
                    modifier = Modifier.weight(1f),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Color(0xFF4CAF50)
                    )
                ) {
                    Text("Na voljo", fontSize = 12.sp)
                }
                Button(
                    onClick = onSetTimeRange,
                    modifier = Modifier.weight(1f),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Color(0xFFFF9800)
                    )
                ) {
                    Text("Delno", fontSize = 12.sp)
                }
                Button(
                    onClick = onSetUnavailable,
                    modifier = Modifier.weight(1f),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Color(0xFFF44336)
                    )
                ) {
                    Text("Ni na voljo", fontSize = 12.sp)
                }
            }
            
            if (showTimeInput) {
                Spacer(modifier = Modifier.height(12.dp))
                Text(
                    text = "Vnesite čas (HH:mm):",
                    fontSize = 14.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Spacer(modifier = Modifier.height(8.dp))
                
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    OutlinedTextField(
                        value = startTime,
                        onValueChange = onStartTimeChange,
                        label = { Text("Od", fontSize = 12.sp) },
                        placeholder = { Text("08:00", fontSize = 12.sp) },
                        modifier = Modifier.weight(1f),
                        singleLine = true
                    )
                    Text(text = "–", fontSize = 18.sp, fontWeight = FontWeight.Bold)
                    OutlinedTextField(
                        value = endTime,
                        onValueChange = onEndTimeChange,
                        label = { Text("Do", fontSize = 12.sp) },
                        placeholder = { Text("15:00", fontSize = 12.sp) },
                        modifier = Modifier.weight(1f),
                        singleLine = true
                    )
                }
                
                Spacer(modifier = Modifier.height(8.dp))
                
                Button(
                    onClick = onConfirmTimeRange,
                    enabled = startTime.isNotBlank() && endTime.isNotBlank(),
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Color(0xFFFF9800)
                    )
                ) {
                    Text("Potrdi čas")
                }
            }
        }
    }
}

@Composable
private fun LegendItem(color: Color, text: String) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        Box(
            modifier = Modifier
                .size(12.dp)
                .background(color, MaterialTheme.shapes.small)
        )
        Text(text = text, fontSize = 12.sp)
    }
}
