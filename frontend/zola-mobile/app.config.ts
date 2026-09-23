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
    package: process.env.ZOLA_ANDROID_PACKAGE ?? 'dev.fookbase.zola',
    versionCode: 1,
    intentFilters: callback ? [{
      action: 'VIEW',
      autoVerify: true,
      category: ['BROWSABLE', 'DEFAULT'],
      data: [{ scheme: 'https', host: callback.host, path: callback.pathname }],
    }] : [],
  },
  ios: {
    bundleIdentifier: process.env.ZOLA_IOS_BUNDLE ?? 'dev.fookbase.zola',
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
  experiments: { typedRoutes: false },
};

export default config;
