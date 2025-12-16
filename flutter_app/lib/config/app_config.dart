import 'package:flutter/foundation.dart' show kIsWeb, kDebugMode;
import 'dart:io' show Platform;

/// App Configuration
/// 
/// IMPORTANT: Update the PRODUCTION_API_URL with your Railway backend URL
class AppConfig {
  // ============================================
  // CONFIGURATION - UPDATE THIS FOR PRODUCTION
  // ============================================
  
  // Production backend URL (Railway deployment)
  // Example: 'https://ride-sync-production.up.railway.app'
  // Get this from Railway dashboard after deployment
  static const String PRODUCTION_API_URL = 'https://ridesyncbackend-production.up.railway.app';
  
  // For local development with physical device
  // Replace with your computer's IP address (e.g., '192.168.1.100')
  static const String BACKEND_IP = 'YOUR_COMPUTER_IP_HERE';
  
  // Backend port for local development (default: 3000)
  static const int BACKEND_PORT = 3000;
  
  // ============================================
  // AUTO-CONFIGURATION (Don't modify below)
  // ============================================
  
  /// Get the base URL for API calls
  static String get apiBaseUrl {
    // Always use Railway.app backend
    return '$PRODUCTION_API_URL/api';
  }
  
  /// Get the WebSocket URL
  static String get wsUrl {
    // Always use Railway.app backend for WebSocket
    // Convert https:// to wss:// for WebSocket secure connection
    return PRODUCTION_API_URL.replaceFirst('https://', 'wss://').replaceFirst('http://', 'ws://');
  }
  
  /// Check if backend is configured for release builds
  static bool get isConfiguredForRelease {
    return PRODUCTION_API_URL.isNotEmpty && PRODUCTION_API_URL != 'https://ridesyncbackend-production.up.railway.app';
  }
  
  /// Check if using production backend
  static bool get isProduction {
    return true; // Always production with Railway
  }
}

