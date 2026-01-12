package Android.availibityCollector.screens

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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import Android.availibityCollector.ui.theme.MyApplicationTheme
import java.time.LocalDate
import java.time.YearMonth
import java.time.format.DateTimeFormatter
import java.time.format.TextStyle
import java.util.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AvailabilityScreen(
    onBackClick: () -> Unit
) {
    var currentMonth by remember { mutableStateOf(YearMonth.now()) }
    var selectedDate by remember { mutableStateOf<LocalDate?>(null) }
    var availabilityType by remember { mutableStateOf("weekly") } // "weekly" or "monthly"
    
    // Track availability for each day (simplified: 0 = not set, 1 = available, 2 = not available, 3 = partial)
    val availability = remember { mutableStateMapOf<LocalDate, Int>() }

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
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {
            // Type selector
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.surfaceVariant
                    )
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(8.dp),
                        horizontalArrangement = Arrangement.SpaceEvenly
                    ) {
                        FilterChip(
                            selected = availabilityType == "weekly",
                            onClick = { availabilityType = "weekly" },
                            label = { Text("Tedenski") }
                        )
                        FilterChip(
                            selected = availabilityType == "monthly",
                            onClick = { availabilityType = "monthly" },
                            label = { Text("Mesečni") }
                        )
                    }
                }
                
                Spacer(modifier = Modifier.height(16.dp))
            }
            
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
                    selectedDate = selectedDate,
                    availability = availability,
                    onDateClick = { date ->
                        selectedDate = date
                    }
                )
                
                Spacer(modifier = Modifier.height(16.dp))
            }
            
            // Selected date details
            item {
                selectedDate?.let { date ->
                    Card(
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(
                            modifier = Modifier.padding(16.dp)
                        ) {
                            Text(
                                text = date.format(DateTimeFormatter.ofPattern("EEEE, d. MMMM yyyy", Locale("sl"))),
                                fontWeight = FontWeight.Bold,
                                fontSize = 16.sp
                            )
                            
                            Spacer(modifier = Modifier.height(12.dp))
                            
                            Text(
                                text = "Označi razpoložljivost:",
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
                                    selected = availability[date] == 1,
                                    onClick = { availability[date] = 1 },
                                    modifier = Modifier.weight(1f)
                                )
                                AvailabilityButton(
                                    text = "Delno",
                                    color = Color(0xFFFF9800),
                                    selected = availability[date] == 3,
                                    onClick = { availability[date] = 3 },
                                    modifier = Modifier.weight(1f)
                                )
                                AvailabilityButton(
                                    text = "Ni na voljo",
                                    color = Color(0xFFF44336),
                                    selected = availability[date] == 2,
                                    onClick = { availability[date] = 2 },
                                    modifier = Modifier.weight(1f)
                                )
                            }
                            
                            // Time slots for partial availability
                            if (availability[date] == 3) {
                                Spacer(modifier = Modifier.height(12.dp))
                                Text(
                                    text = "Izberite ure:",
                                    fontSize = 14.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                Spacer(modifier = Modifier.height(8.dp))
                                // Time slot selector would go here
                                Text(
                                    text = "8:00 - 12:00, 14:00 - 18:00",
                                    fontSize = 12.sp,
                                    color = MaterialTheme.colorScheme.primary
                                )
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
                        // TODO: Submit availability to API
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(50.dp)
                ) {
                    Text(
                        text = "Shrani razpoložljivost",
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Medium
                    )
                }
                
                Spacer(modifier = Modifier.height(32.dp))
            }
        }
    }
}

@Composable
private fun CalendarGrid(
    yearMonth: YearMonth,
    selectedDate: LocalDate?,
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
                        val isSelected = date == selectedDate
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

@Preview(showBackground = true)
@Composable
fun AvailabilityScreenPreview() {
    MyApplicationTheme {
        AvailabilityScreen(onBackClick = {})
    }
}
