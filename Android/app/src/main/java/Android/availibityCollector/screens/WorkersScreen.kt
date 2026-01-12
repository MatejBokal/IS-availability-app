package Android.availibityCollector.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Person
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import Android.availibityCollector.ui.theme.MyApplicationTheme

data class Worker(
    val id: Int,
    val ime: String,
    val priimek: String,
    val delovnoMesto: String,
    val vrstaZaposlitve: String,
    val isActive: Boolean
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WorkersScreen(
    onBackClick: () -> Unit
) {
    // Sample data - would come from API
    val workers = remember {
        listOf(
            Worker(1, "Janez", "Novak", "Prodajalec", "Študent", true),
            Worker(2, "Maja", "Horvat", "Blagajnik", "Študent", true),
            Worker(3, "Peter", "Krajnc", "Skladiščnik", "Redno zaposleni", true),
            Worker(4, "Ana", "Zupan", "Prodajalec", "Študent", false)
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Delavci") },
                navigationIcon = {
                    IconButton(onClick = onBackClick) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Nazaj"
                        )
                    }
                }
            )
        },
        floatingActionButton = {
            FloatingActionButton(
                onClick = { /* TODO: Add new worker */ }
            ) {
                Icon(Icons.Filled.Add, contentDescription = "Dodaj delavca")
            }
        }
    ) { paddingValues ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Text(
                    text = "Seznam delavcev",
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Bold
                )
                Spacer(modifier = Modifier.height(8.dp))
            }
            
            items(workers) { worker ->
                WorkerCard(worker = worker)
            }
        }
    }
}

@Composable
private fun WorkerCard(worker: Worker) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = if (worker.isActive) 
                MaterialTheme.colorScheme.surface 
            else 
                MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = Icons.Filled.Person,
                contentDescription = null,
                modifier = Modifier.size(40.dp),
                tint = if (worker.isActive) 
                    MaterialTheme.colorScheme.primary 
                else 
                    MaterialTheme.colorScheme.onSurfaceVariant
            )
            
            Spacer(modifier = Modifier.width(16.dp))
            
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = "${worker.ime} ${worker.priimek}",
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 16.sp
                )
                Text(
                    text = worker.delovnoMesto,
                    fontSize = 14.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = worker.vrstaZaposlitve,
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            
            if (!worker.isActive) {
                AssistChip(
                    onClick = { },
                    label = { Text("Neaktiven", fontSize = 10.sp) }
                )
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
fun WorkersScreenPreview() {
    MyApplicationTheme {
        WorkersScreen(onBackClick = {})
    }
}
