import type { ExpoConfig } from 'expo/config';

const callback = process.env.EXPO_PUBLIC_MOBILE_CALLBACK_URL ? new URL(process.env.EXPO_PUBLIC_MOBILE_CALLBACK_URL) : null;
if (callback && (callback.protocol !== 'https:' || callback.search || callback.hash || callback.username || callback.password)) {
  throw new Error('Mobile callback must be a fixed HTTPS URL.');
}

const config: ExpoConfig = {
  name: 'Zola',
  slug: 'zola-mobile',
  version: '1.0.0',
  scheme: 'zola',
  userInterfaceStyle: 'automatic',
  android: {
    package: process.env.ZOLA_ANDROID_PACKAGE ?? 'com.fookbase.zola',
    googleServicesFile: './google-services.json',
    versionCode: 2,
    intentFilters: callback ? [{
      action: 'VIEW',
      autoVerify: true,
      category: ['BROWSABLE', 'DEFAULT'],
      data: [{ scheme: 'https', host: callback.host, path: callback.pathname }],
    }] : [],
  },
  ios: {
    bundleIdentifier: process.env.ZOLA_IOS_BUNDLE ?? 'com.fookbase.zola',
    supportsTablet: true,
  },
  plugins: [
    'expo-router',
    'expo-secure-store',
    'expo-video',
    'expo-notifications',
    ['expo-image-picker', {
      photosPermission: 'Cho phép Zola chọn ảnh và video để gửi trong cuộc trò chuyện.',
      cameraPermission: false,
      microphonePermission: false,
    }],
  ],
  extra: {
    eas: { projectId: 'b3ae7927-d1b5-4a50-b269-79c0061e71d3' },
  },
  experiments: { typedRoutes: false },
};

export default config;
