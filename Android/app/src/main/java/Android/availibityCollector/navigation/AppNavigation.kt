package Android.availibityCollector.navigation

import androidx.compose.runtime.*
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import Android.availibityCollector.screens.*

sealed class Screen(val route: String) {
    object Landing : Screen("landing")
    object Login : Screen("login")
    object Register : Screen("register")
    object Home : Screen("home/{email}") {
        fun createRoute(email: String) = "home/${java.net.URLEncoder.encode(email, "UTF-8")}"
    }
    object Availability : Screen("availability")
    object Workers : Screen("workers")
    object Profile : Screen("profile/{email}") {
        fun createRoute(email: String) = "profile/${java.net.URLEncoder.encode(email, "UTF-8")}"
    }
}

@Composable
fun AppNavigation(navController: NavHostController) {
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
                    loggedInEmail = ""
                    navController.navigate(Screen.Landing.route) {
                        popUpTo(0) { inclusive = true }
                    }
                },
                onProfileClick = {
                    navController.navigate(Screen.Profile.createRoute(email))
                },
                onAvailabilityClick = {
                    navController.navigate(Screen.Availability.route)
                },
                onWorkersClick = {
                    navController.navigate(Screen.Workers.route)
                }
            )
        }
        composable(Screen.Availability.route) {
            AvailabilityScreen(
                onBackClick = { navController.popBackStack() }
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
                    loggedInEmail = ""
                    navController.navigate(Screen.Landing.route) {
                        popUpTo(0) { inclusive = true }
                    }
                }
            )
        }
    }
}
