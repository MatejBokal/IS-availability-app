package Android.availibityCollector.screens

import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.ChevronLeft
import androidx.compose.material.icons.filled.ChevronRight
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
import com.google.gson.Gson
import java.time.LocalDate
import java.time.YearMonth
import java.time.format.DateTimeFormatter
import java.util.*

/**
 * AvailabilityScreen - CREATE operation using Volley
 * Workers can submit their availability for scheduling
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AvailabilityScreen(
    onBackClick: () -> Unit
) {
    val context = LocalContext.current
    val volleyClient = remember { VolleyClient.getInstance(context) }
    val gson = remember { Gson() }
    
    var currentMonth by remember { mutableStateOf(YearMonth.now()) }
    // Allow multiple date selection
    var selectedDates by remember { mutableStateOf(setOf<LocalDate>()) }
    var isSaving by remember { mutableStateOf(false) }
    var isLoading by remember { mutableStateOf(true) }
    
    // Track availability for each day (simplified: 0 = not set, 1 = available, 2 = not available, 3 = partial)
    val availability = remember { mutableStateMapOf<LocalDate, Int>() }
    
    // Track custom time range for partial availability (per date) - stores "startTime - endTime"
    val timeSlots = remember { mutableStateMapOf<LocalDate, MutableSet<String>>() }
    
    // State for custom time input
    var customStartTime by remember { mutableStateOf("") }
    var customEndTime by remember { mutableStateOf("") }
    
    // Load existing availability on screen open
    LaunchedEffect(Unit) {
        volleyClient.getRazpolozljivostByWorker(
            workerId = 1, // TODO: Get from logged-in user
            onSuccess = { razpolozljivosti ->
                // Parse saved availability and populate the calendar
                razpolozljivosti.forEach { razpolozljivost ->
                    try {
                        // Parse the JSON containing date -> status mappings
                        val typeToken = object : com.google.gson.reflect.TypeToken<Map<String, Map<String, Any>>>() {}.type
                        val savedData: Map<String, Map<String, Any>> = gson.fromJson(razpolozljivost.razpolozljivostJSON, typeToken)
                        
                        savedData.forEach { (dateStr, data) ->
                            val date = LocalDate.parse(dateStr)
                            val status = (data["status"] as? Double)?.toInt() ?: 0
                            availability[date] = status
                            
                            // Also load time slots if available
                            @Suppress("UNCHECKED_CAST")
                            val slots = data["timeSlots"] as? List<String>
                            if (!slots.isNullOrEmpty()) {
                                timeSlots[date] = slots.toMutableSet()
                            }
                        }
                    } catch (e: Exception) {
                        // Ignore parsing errors for individual entries
                    }
                }
                isLoading = false
            },
            onError = { _ ->
                // If loading fails, just continue with empty availability
                isLoading = false
            }
        )
    }
    
    // Helper function to apply availability to all selected days
    fun applyToSelectedDays(status: Int) {
        selectedDates.forEach { date ->
            availability[date] = status
        }
    }
    
    // Helper function to apply custom time range to all selected days
    fun applyCustomTimeToSelectedDays(startTime: String, endTime: String) {
        if (startTime.isNotBlank() && endTime.isNotBlank()) {
            val timeRange = "$startTime - $endTime"
            selectedDates.forEach { date ->
                timeSlots[date] = mutableSetOf(timeRange)
            }
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Razpoložljivost") },
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
            // Show loading indicator while fetching existing availability
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
            
            // Month navigation
            item {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    IconButton(onClick = { currentMonth = currentMonth.minusMonths(1) }) {
                        Icon(Icons.Filled.ChevronLeft, contentDescription = "Prejšnji mesec")
                    }
                    
                    Text(
                        text = currentMonth.format(DateTimeFormatter.ofPattern("LLLL yyyy", Locale("sl"))),
                        fontSize = 18.sp,
                        fontWeight = FontWeight.Bold
                    )
                    
                    IconButton(onClick = { currentMonth = currentMonth.plusMonths(1) }) {
                        Icon(Icons.Filled.ChevronRight, contentDescription = "Naslednji mesec")
                    }
                }
                
                Spacer(modifier = Modifier.height(8.dp))
            }
            
            // Day headers
            item {
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
            }
            
            // Calendar grid
            item {
                CalendarGrid(
                    yearMonth = currentMonth,
                    selectedDates = selectedDates,
                    availability = availability,
                    onDateClick = { date ->
                        // Toggle selection on click
                        selectedDates = if (selectedDates.contains(date)) {
                            selectedDates - date
                        } else {
                            selectedDates + date
                        }
                    }
                )
                
                Spacer(modifier = Modifier.height(8.dp))
                
                // Show selected count and clear button
                if (selectedDates.isNotEmpty()) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Izbrano: ${selectedDates.size} dni",
                            fontSize = 14.sp,
                            color = MaterialTheme.colorScheme.primary,
                            fontWeight = FontWeight.Medium
                        )
                        TextButton(onClick = { selectedDates = emptySet() }) {
                            Text("Počisti izbor")
                        }
                    }
                }
                
                Spacer(modifier = Modifier.height(16.dp))
            }
            
            // Availability controls for selected dates
            item {
                if (selectedDates.isNotEmpty()) {
                    Card(
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(
                            modifier = Modifier.padding(16.dp)
                        ) {
                            // Show which dates are selected
                            val sortedDates = selectedDates.sorted()
                            val displayText = if (sortedDates.size == 1) {
                                sortedDates[0].format(DateTimeFormatter.ofPattern("EEEE, d. MMMM", Locale("sl")))
                            } else {
                                "${sortedDates.size} izbranih dni"
                            }
                            
                            Text(
                                text = displayText,
                                fontWeight = FontWeight.Bold,
                                fontSize = 16.sp
                            )
                            
                            if (sortedDates.size > 1) {
                                Spacer(modifier = Modifier.height(4.dp))
                                Text(
                                    text = sortedDates.take(5).joinToString(", ") { 
                                        it.format(DateTimeFormatter.ofPattern("d. MMM", Locale("sl"))) 
                                    } + if (sortedDates.size > 5) ", ..." else "",
                                    fontSize = 12.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
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
                                AvailabilityButton(
                                    text = "Na voljo",
                                    color = Color(0xFF4CAF50),
                                    selected = false,
                                    onClick = { 
                                        applyToSelectedDays(1)
                                        selectedDates = emptySet()
                                    },
                                    modifier = Modifier.weight(1f)
                                )
                                AvailabilityButton(
                                    text = "Delno",
                                    color = Color(0xFFFF9800),
                                    selected = false,
                                    onClick = { 
                                        applyToSelectedDays(3)
                                        // Don't clear selection - user needs to enter time first
                                    },
                                    modifier = Modifier.weight(1f)
                                )
                                AvailabilityButton(
                                    text = "Ni na voljo",
                                    color = Color(0xFFF44336),
                                    selected = false,
                                    onClick = { 
                                        applyToSelectedDays(2)
                                        selectedDates = emptySet()
                                    },
                                    modifier = Modifier.weight(1f)
                                )
                            }
                            
                            // Time input for partial availability (shown if any selected date has partial)
                            val anyPartial = selectedDates.any { availability[it] == 3 }
                            if (anyPartial) {
                                Spacer(modifier = Modifier.height(12.dp))
                                Text(
                                    text = "Vnesite čas (npr. 8:00, 14:30):",
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
                                        value = customStartTime,
                                        onValueChange = { customStartTime = it },
                                        label = { Text("Od", fontSize = 12.sp) },
                                        placeholder = { Text("8:00", fontSize = 12.sp) },
                                        modifier = Modifier.weight(1f),
                                        singleLine = true
                                    )
                                    Text(
                                        text = "–",
                                        fontSize = 18.sp,
                                        fontWeight = FontWeight.Bold
                                    )
                                    OutlinedTextField(
                                        value = customEndTime,
                                        onValueChange = { customEndTime = it },
                                        label = { Text("Do", fontSize = 12.sp) },
                                        placeholder = { Text("15:00", fontSize = 12.sp) },
                                        modifier = Modifier.weight(1f),
                                        singleLine = true
                                    )
                                }
                                
                                Spacer(modifier = Modifier.height(8.dp))
                                
                                Button(
                                    onClick = {
                                        applyCustomTimeToSelectedDays(customStartTime, customEndTime)
                                        selectedDates = emptySet()
                                        customStartTime = ""
                                        customEndTime = ""
                                    },
                                    enabled = customStartTime.isNotBlank() && customEndTime.isNotBlank(),
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
            item {
                Button(
                    onClick = {
                        // CREATE operation - Save availability using Volley
                        if (availability.isNotEmpty()) {
                            isSaving = true
                            
                            // Convert availability map to JSON (including time slots for partial days)
                            val availabilityData = availability.map { (date, status) ->
                                val dateStr = date.toString()
                                val slots = if (status == 3) timeSlots[date]?.toList() ?: emptyList() else emptyList()
                                dateStr to mapOf(
                                    "status" to status,
                                    "timeSlots" to slots
                                )
                            }.toMap()
                            val razpolozljivostJSON = gson.toJson(availabilityData)
                            val mesecLeto = currentMonth.format(DateTimeFormatter.ofPattern("MM/yyyy"))
                            
                            // For demo purposes, using workerId = 1
                            // In real app, this would come from logged-in user
                            volleyClient.createRazpolozljivost(
                                workerId = 1,
                                razpolozljivostJSON = razpolozljivostJSON,
                                mesecLeto = mesecLeto,
                                type = "monthly",
                                zaporedniTeden = null,
                                onSuccess = { _ ->
                                    isSaving = false
                                    Toast.makeText(
                                        context, 
                                        "Razpoložljivost uspešno shranjena!", 
                                        Toast.LENGTH_SHORT
                                    ).show()
                                },
                                onError = { error ->
                                    isSaving = false
                                    Toast.makeText(
                                        context, 
                                        "Napaka: $error", 
                                        Toast.LENGTH_LONG
                                    ).show()
                                }
                            )
                        } else {
                            Toast.makeText(
                                context, 
                                "Označite vsaj en dan", 
                                Toast.LENGTH_SHORT
                            ).show()
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
        } // end else (isLoading)
    }
}

@Composable
private fun CalendarGrid(
    yearMonth: YearMonth,
    selectedDates: Set<LocalDate>,
    availability: Map<LocalDate, Int>,
    onDateClick: (LocalDate) -> Unit
) {
    val firstDayOfMonth = yearMonth.atDay(1)
    val lastDayOfMonth = yearMonth.atEndOfMonth()
    val firstDayOfWeek = (firstDayOfMonth.dayOfWeek.value - 1) // Monday = 0
    val daysInMonth = yearMonth.lengthOfMonth()
    
    val totalCells = firstDayOfWeek + daysInMonth
    val rows = (totalCells + 6) / 7
    
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
                        val availabilityStatus = availability[date] ?: 0
                        
                        val backgroundColor = when (availabilityStatus) {
                            1 -> Color(0xFF4CAF50).copy(alpha = 0.3f)
                            2 -> Color(0xFFF44336).copy(alpha = 0.3f)
                            3 -> Color(0xFFFF9800).copy(alpha = 0.3f)
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
                            Text(
                                text = dayOfMonth.toString(),
                                fontSize = 14.sp,
                                fontWeight = if (date == LocalDate.now()) FontWeight.Bold else FontWeight.Normal,
                                color = if (date == LocalDate.now()) 
                                    MaterialTheme.colorScheme.primary 
                                else 
                                    MaterialTheme.colorScheme.onSurface
                            )
                        }
                    } else {
                        Spacer(modifier = Modifier.weight(1f))
                    }
                }
            }
        }
    }
}

@Composable
private fun AvailabilityButton(
    text: String,
    color: Color,
    selected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Button(
        onClick = onClick,
        modifier = modifier.height(40.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = if (selected) color else color.copy(alpha = 0.2f),
            contentColor = if (selected) Color.White else color
        )
    ) {
        Text(text = text, fontSize = 12.sp)
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
