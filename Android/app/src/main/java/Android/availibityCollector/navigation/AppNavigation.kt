package Android.availibityCollector.navigation

import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import Android.availibityCollector.screens.*
import Android.availibityCollector.data.api.VolleyClient

sealed class Screen(val route: String) {
    object Landing : Screen("landing")
    object Login : Screen("login")
    object Register : Screen("register")
    object Home : Screen("home/{email}") {
        fun createRoute(email: String) = "home/${java.net.URLEncoder.encode(email, "UTF-8")}"
    }
    object MonthSelection : Screen("month-selection")
    object Availability : Screen("availability/{monthKey}") {
        fun createRoute(monthKey: String) = "availability/${java.net.URLEncoder.encode(monthKey, "UTF-8")}"
    }
    object History : Screen("history")
    object HistoryMonth : Screen("history/{monthKey}") {
        fun createRoute(monthKey: String) = "history/${java.net.URLEncoder.encode(monthKey, "UTF-8")}"
    }
    object Workers : Screen("workers")
    object Profile : Screen("profile/{email}") {
        fun createRoute(email: String) = "profile/${java.net.URLEncoder.encode(email, "UTF-8")}"
    }
}

@Composable
fun AppNavigation(navController: NavHostController) {
    val context = LocalContext.current
    val volleyClient = remember { VolleyClient.getInstance(context) }
    
    // Store logged in user email
    var loggedInEmail by remember { mutableStateOf("") }
    
    NavHost(
        navController = navController,
        startDestination = Screen.Landing.route
    ) {
        composable(Screen.Landing.route) {
            LandingScreen(
                onLoginClick = { navController.navigate(Screen.Login.route) },
                onRegisterClick = { navController.navigate(Screen.Register.route) }
            )
        }
        composable(Screen.Login.route) {
            LoginScreen(
                onBackClick = { navController.popBackStack() },
                onLoginSuccess = { email ->
                    loggedInEmail = email
                    navController.navigate(Screen.Home.createRoute(email)) {
                        popUpTo(Screen.Landing.route) { inclusive = true }
                    }
                },
                onRegisterClick = { 
                    navController.navigate(Screen.Register.route) {
                        popUpTo(Screen.Landing.route)
                    }
                }
            )
        }
        composable(Screen.Register.route) {
            RegisterScreen(
                onBackClick = { navController.popBackStack() },
                onRegisterSuccess = { 
                    navController.navigate(Screen.Login.route) {
                        popUpTo(Screen.Landing.route)
                    }
                },
                onLoginClick = { 
                    navController.navigate(Screen.Login.route) {
                        popUpTo(Screen.Landing.route)
                    }
                }
            )
        }
        composable(Screen.Home.route) { backStackEntry ->
            val email = backStackEntry.arguments?.getString("email")?.let {
                java.net.URLDecoder.decode(it, "UTF-8")
            } ?: loggedInEmail
            
            HomeScreen(
                userEmail = email,
                onLogoutClick = {
                    volleyClient.logout()
                    loggedInEmail = ""
                    navController.navigate(Screen.Landing.route) {
                        popUpTo(0) { inclusive = true }
                    }
                },
                onProfileClick = {
                    navController.navigate(Screen.Profile.createRoute(email))
                },
                onAvailabilityClick = {
                    navController.navigate(Screen.MonthSelection.route)
                },
                onHistoryClick = {
                    navController.navigate(Screen.History.route)
                },
                onWorkersClick = {
                    navController.navigate(Screen.Workers.route)
                }
            )
        }
        composable(Screen.MonthSelection.route) {
            MonthSelectionScreen(
                onBackClick = { navController.popBackStack() },
                onMonthSelected = { monthKey ->
                    navController.navigate(Screen.Availability.createRoute(monthKey))
                }
            )
        }
        composable(Screen.Availability.route) { backStackEntry ->
            val monthKey = backStackEntry.arguments?.getString("monthKey")?.let {
                java.net.URLDecoder.decode(it, "UTF-8")
            } ?: ""
            
            AvailabilityScreen(
                monthKey = monthKey,
                onBackClick = { navController.popBackStack() }
            )
        }
        composable(Screen.History.route) {
            HistoryScreen(
                onBackClick = { navController.popBackStack() },
                onMonthSelected = { monthKey ->
                    navController.navigate(Screen.HistoryMonth.createRoute(monthKey))
                }
            )
        }
        composable(Screen.HistoryMonth.route) { backStackEntry ->
            val monthKey = backStackEntry.arguments?.getString("monthKey")?.let {
                java.net.URLDecoder.decode(it, "UTF-8")
            } ?: ""
            
            // Show AvailabilityScreen in read-only mode for history
            AvailabilityScreen(
                monthKey = monthKey,
                onBackClick = { navController.popBackStack() },
                readOnly = true
            )
        }
        composable(Screen.Workers.route) {
            WorkersScreen(
                onBackClick = { navController.popBackStack() }
            )
        }
        composable(Screen.Profile.route) { backStackEntry ->
            val email = backStackEntry.arguments?.getString("email")?.let {
                java.net.URLDecoder.decode(it, "UTF-8")
            } ?: loggedInEmail
            
            ProfileScreen(
                userEmail = email,
                onBackClick = { navController.popBackStack() },
                onLogoutClick = {
                    volleyClient.logout()
                    loggedInEmail = ""
                    navController.navigate(Screen.Landing.route) {
                        popUpTo(0) { inclusive = true }
                    }
                }
            )
        }
    }
}
