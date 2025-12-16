# Ride Sync Flutter App

A beautiful and modern ride-sharing mobile application built with Flutter.

## Prerequisites

1. **Flutter SDK**: Install Flutter SDK (version 3.0.0 or higher)
   - Download from: https://flutter.dev/docs/get-started/install
   - Verify installation: `flutter doctor`

2. **Android Studio** (for Android development)
   - Download from: https://developer.android.com/studio

3. **Backend Server**: Make sure the backend server is running
   - See `../backend/README.md` for setup instructions

## Setup Instructions

1. **Navigate to the Flutter app directory**
   ```bash
   cd flutter_app
   ```

2. **Install dependencies**
   ```bash
   flutter pub get
   ```

3. **Configure Backend URL**
   
   Update the backend URL in the following files based on your setup:
   
   - **For Android Emulator**: Use `http://10.0.2.2:3000`
   - **For iOS Simulator**: Use `http://localhost:3000`
   - **For Physical Device**: Use `http://YOUR_COMPUTER_IP:3000`
   
   Files to update:
   - `lib/services/api_service.dart` - Update `baseUrl`
   - `lib/services/websocket_service.dart` - Update `wsUrl`
   
   To find your computer's IP address:
   - Windows: `ipconfig` (look for IPv4 Address)
   - Mac/Linux: `ifconfig` or `ip addr`

4. **Run the app**
   ```bash
   flutter run
   ```

## Building Release APK

### Step 1: Prepare for Release

1. **Update app configuration** (if needed):
   - Edit `android/app/build.gradle` to change `applicationId`, `versionCode`, or `versionName`

2. **Create a keystore** (for signing the APK):
   ```bash
   keytool -genkey -v -keystore ~/upload-keystore.jks -keyalg RSA -keysize 2048 -validity 10000 -alias upload
   ```
   
   Note: Keep the keystore file and password safe!

3. **Create key.properties file**:
   Create `android/key.properties` with:
   ```
   storePassword=<your-keystore-password>
   keyPassword=<your-key-password>
   keyAlias=upload
   storeFile=<path-to-keystore-file>
   ```

### Step 2: Build Release APK

**Option A: Build APK directly**
```bash
cd flutter_app
flutter build apk --release
```

The APK will be generated at:
`flutter_app/build/app/outputs/flutter-apk/app-release.apk`

**Option B: Build App Bundle (for Google Play Store)**
```bash
flutter build appbundle --release
```

The AAB will be generated at:
`flutter_app/build/app/outputs/bundle/release/app-release.aab`

### Step 3: Install APK on Physical Device

**Method 1: Using ADB (Android Debug Bridge)**
1. Enable USB Debugging on your Android device:
   - Go to Settings > About Phone
   - Tap "Build Number" 7 times to enable Developer Options
   - Go to Settings > Developer Options
   - Enable "USB Debugging"

2. Connect your device via USB

3. Verify connection:
   ```bash
   adb devices
   ```

4. Install the APK:
   ```bash
   adb install build/app/outputs/flutter-apk/app-release.apk
   ```

**Method 2: Transfer and Install Manually**
1. Copy `app-release.apk` to your Android device (via USB, email, or cloud storage)

2. On your device:
   - Open File Manager
   - Navigate to the APK file
   - Tap the APK file
   - Allow installation from unknown sources if prompted
   - Tap "Install"

**Method 3: Using Flutter Install Command**
```bash
flutter install --release
```

## Project Structure

```
flutter_app/
├── lib/
│   ├── main.dart                 # App entry point
│   ├── models/                   # Data models
│   │   ├── user_model.dart
│   │   └── ride_model.dart
│   ├── services/                 # API and WebSocket services
│   │   ├── api_service.dart
│   │   └── websocket_service.dart
│   ├── providers/                # State management
│   │   ├── auth_provider.dart
│   │   └── ride_provider.dart
│   └── screens/                  # UI screens
│       ├── splash_screen.dart
│       ├── auth/
│       │   ├── welcome_screen.dart
│       │   ├── rider_auth_screen.dart
│       │   └── driver_auth_screen.dart
│       ├── rider/
│       │   ├── rider_home_screen.dart
│       │   ├── request_ride_screen.dart
│       │   ├── ride_status_screen.dart
│       │   └── ride_history_screen.dart
│       └── driver/
│           ├── driver_home_screen.dart
│           ├── available_rides_screen.dart
│           └── driver_ride_status_screen.dart
├── android/                      # Android configuration
├── pubspec.yaml                  # Dependencies
└── README.md                     # This file
```

## Features

### Rider Features
- ✅ User registration and login
- ✅ Request rides with pickup and drop locations
- ✅ Real-time ride status updates
- ✅ View ride history
- ✅ Beautiful and intuitive UI

### Driver Features
- ✅ Driver registration and login
- ✅ View available ride requests
- ✅ Accept ride requests
- ✅ Update ride status (Arrived, In Progress, Completed)
- ✅ Toggle availability status
- ✅ Real-time ride notifications

## Troubleshooting

### Common Issues

1. **Connection Error to Backend**
   - Ensure backend server is running
   - Check if the IP address/URL is correct
   - Verify firewall settings allow connections

2. **Location Permission Denied**
   - Go to device Settings > Apps > Ride Sync > Permissions
   - Enable Location permission

3. **Build Errors**
   - Run `flutter clean`
   - Run `flutter pub get`
   - Ensure all dependencies are properly installed

4. **APK Installation Failed**
   - Enable "Install from Unknown Sources" in device settings
   - Check if device has enough storage space
   - Ensure the APK is not corrupted

## Development Notes

- The app uses Provider for state management
- WebSocket is used for real-time updates
- Location services require proper permissions
- Make sure to update backend URLs before building for production

## License

This project is for academic/educational purposes.

