package Android.availibityCollector.screens

import android.widget.Toast
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import Android.availibityCollector.data.api.VolleyClient
import Android.availibityCollector.data.models.AvailabilitySubmissionDto
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HistoryScreen(
    onBackClick: () -> Unit,
    onMonthSelected: (String) -> Unit  // monthKey
) {
    val context = LocalContext.current
    val volleyClient = remember { VolleyClient.getInstance(context) }
    
    var submissions by remember { mutableStateOf<List<AvailabilitySubmissionDto>>(emptyList()) }
    var isLoading by remember { mutableStateOf(true) }
    
    LaunchedEffect(Unit) {
        volleyClient.getMySubmissions(
            onSuccess = { submissionList ->
                submissions = submissionList.sortedByDescending { it.monthKey }
                isLoading = false
            },
            onError = { error ->
                Toast.makeText(context, "Napaka: $error", Toast.LENGTH_LONG).show()
                isLoading = false
            }
        )
    }
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Zgodovina razpoložljivosti") },
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
                    Text("Nalagam zgodovino...")
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(16.dp)
            ) {
                if (submissions.isEmpty()) {
                    item {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.surfaceVariant
                            )
                        ) {
                            Column(
                                modifier = Modifier.padding(24.dp),
                                horizontalAlignment = Alignment.CenterHorizontally
                            ) {
                                Icon(
                                    imageVector = Icons.Filled.CalendarToday,
                                    contentDescription = null,
                                    modifier = Modifier.size(48.dp),
                                    tint = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                Spacer(modifier = Modifier.height(16.dp))
                                Text(
                                    text = "Nimate še oddanih razpoložljivosti.",
                                    textAlign = androidx.compose.ui.text.style.TextAlign.Center,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }
                } else {
                    items(submissions) { submission ->
                        HistoryItem(
                            submission = submission,
                            onClick = { onMonthSelected(submission.monthKey) }
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                    }
                }
            }
        }
    }
}

@Composable
private fun HistoryItem(
    submission: AvailabilitySubmissionDto,
    onClick: () -> Unit
) {
    // Parse monthKey to display friendly name
    val monthName = try {
        val parts = submission.monthKey.split("-")
        val monthNum = parts[0].toInt()
        val year = parts[1].toInt()
        val date = LocalDate.of(year, monthNum, 1)
        date.format(DateTimeFormatter.ofPattern("MMMM yyyy", Locale("sl")))
    } catch (e: Exception) {
        submission.monthKey
    }
    
    // Format submission date
    val submittedDate = try {
        submission.submittedAtUtc?.let {
            val instant = Instant.parse(it)
            val localDate = instant.atZone(ZoneId.systemDefault()).toLocalDate()
            localDate.format(DateTimeFormatter.ofPattern("dd.MM.yyyy HH:mm", Locale("sl")))
        } ?: "Neznano"
    } catch (e: Exception) {
        "Neznano"
    }
    
    Card(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = monthName,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Medium
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = "Oddano: $submittedDate",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            
            Icon(
                imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}
