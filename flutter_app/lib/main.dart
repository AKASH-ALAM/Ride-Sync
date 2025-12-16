import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:google_fonts/google_fonts.dart';
import 'providers/auth_provider.dart';
import 'providers/ride_provider.dart';
import 'providers/wallet_provider.dart';
import 'services/websocket_service.dart';
import 'screens/splash_screen.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
      return MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => AuthProvider()),
          ChangeNotifierProvider(create: (_) => RideProvider()),
          ChangeNotifierProvider(create: (_) => WalletProvider()),
          ChangeNotifierProvider(create: (_) => WebSocketService()),
        ],
      child: MaterialApp(
        title: 'Ride Sync',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          // Material 3 Color Scheme - Deep Blue/Teal Primary, Soft Orange/Green Accent
          colorScheme: ColorScheme.fromSeed(
            seedColor: const Color(0xFF0277BD), // Deep blue
            brightness: Brightness.light,
            primary: const Color(0xFF0277BD), // Deep blue
            onPrimary: Colors.white,
            primaryContainer: const Color(0xFFB3E5FC), // Light blue container
            onPrimaryContainer: const Color(0xFF01579B),
            secondary: const Color(0xFFFF9800), // Soft orange accent
            onSecondary: Colors.white,
            secondaryContainer: const Color(0xFFFFE0B2),
            onSecondaryContainer: const Color(0xFFE65100),
            tertiary: const Color(0xFF4CAF50), // Soft green accent
            onTertiary: Colors.white,
            tertiaryContainer: const Color(0xFFC8E6C9),
            onTertiaryContainer: const Color(0xFF1B5E20),
            error: const Color(0xFFD32F2F),
            onError: Colors.white,
            surface: Colors.white,
            onSurface: const Color(0xFF1A1A1A),
            surfaceContainerHighest: const Color(0xFFF7F8FA), // Neutral background
            outline: const Color(0xFFE0E0E0),
            outlineVariant: const Color(0xFFF5F5F5),
          ),
          useMaterial3: true,
          scaffoldBackgroundColor: const Color(0xFFF7F8FA), // Neutral background
          // Typography - Inter font (modern, clean)
          textTheme: GoogleFonts.interTextTheme().apply(
            bodyColor: const Color(0xFF1A1A1A),
            displayColor: const Color(0xFF1A1A1A),
          ).copyWith(
            // Headings: semi-bold
            headlineLarge: GoogleFonts.inter(
              fontSize: 32,
              fontWeight: FontWeight.w600,
              letterSpacing: -0.5,
            ),
            headlineMedium: GoogleFonts.inter(
              fontSize: 28,
              fontWeight: FontWeight.w600,
              letterSpacing: -0.5,
            ),
            headlineSmall: GoogleFonts.inter(
              fontSize: 24,
              fontWeight: FontWeight.w600,
            ),
            titleLarge: GoogleFonts.inter(
              fontSize: 22,
              fontWeight: FontWeight.w600,
            ),
            titleMedium: GoogleFonts.inter(
              fontSize: 18,
              fontWeight: FontWeight.w600,
            ),
            titleSmall: GoogleFonts.inter(
              fontSize: 16,
              fontWeight: FontWeight.w600,
            ),
            // Body text: regular
            bodyLarge: GoogleFonts.inter(
              fontSize: 16,
              fontWeight: FontWeight.w400,
            ),
            bodyMedium: GoogleFonts.inter(
              fontSize: 14,
              fontWeight: FontWeight.w400,
            ),
            bodySmall: GoogleFonts.inter(
              fontSize: 12,
              fontWeight: FontWeight.w400,
            ),
          ),
          appBarTheme: AppBarTheme(
            centerTitle: true,
            elevation: 0,
            scrolledUnderElevation: 1,
            backgroundColor: Colors.white,
            foregroundColor: const Color(0xFF1A1A1A),
            surfaceTintColor: Colors.transparent,
            titleTextStyle: GoogleFonts.inter(
              fontSize: 20,
              fontWeight: FontWeight.w600,
              color: const Color(0xFF1A1A1A),
            ),
            iconTheme: const IconThemeData(color: Color(0xFF1A1A1A)),
          ),
          elevatedButtonTheme: ElevatedButtonThemeData(
            style: ElevatedButton.styleFrom(
              padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 18),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14), // 12-16 radius
              ),
              elevation: 0,
              shadowColor: Colors.transparent,
              backgroundColor: const Color(0xFF0277BD),
              foregroundColor: Colors.white,
              textStyle: GoogleFonts.inter(
                fontSize: 16,
                fontWeight: FontWeight.w600,
                letterSpacing: 0.2,
              ),
            ),
          ),
          outlinedButtonTheme: OutlinedButtonThemeData(
            style: OutlinedButton.styleFrom(
              padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 18),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14),
              ),
              side: const BorderSide(color: Color(0xFF0277BD), width: 1.5),
              foregroundColor: const Color(0xFF0277BD),
              textStyle: GoogleFonts.inter(
                fontSize: 16,
                fontWeight: FontWeight.w600,
                letterSpacing: 0.2,
              ),
            ),
          ),
          inputDecorationTheme: InputDecorationTheme(
            filled: true,
            fillColor: Colors.white,
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: BorderSide(color: Colors.grey[300]!, width: 1),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: BorderSide(color: Colors.grey[300]!, width: 1),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: const BorderSide(color: Color(0xFF0277BD), width: 2),
            ),
            errorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: const BorderSide(color: Color(0xFFD32F2F), width: 1.5),
            ),
            contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 18),
            labelStyle: GoogleFonts.inter(color: Colors.grey[600], fontWeight: FontWeight.w400),
            hintStyle: GoogleFonts.inter(color: Colors.grey[400], fontWeight: FontWeight.w400),
          ),
          cardTheme: CardThemeData(
            elevation: 0,
            shadowColor: Colors.transparent,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
              side: BorderSide(color: Colors.grey[200]!, width: 1),
            ),
            color: Colors.white,
            surfaceTintColor: Colors.transparent,
          ),
        ),
        home: const SplashScreen(),
      ),
    );
  }
}

