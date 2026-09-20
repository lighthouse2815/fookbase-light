import type { ExpoConfig } from 'expo/config';

const callback = process.env.EXPO_PUBLIC_MOBILE_CALLBACK_URL ? new URL(process.env.EXPO_PUBLIC_MOBILE_CALLBACK_URL) : null;
if (callback && (callback.protocol !== 'https:' || callback.search || callback.hash || callback.username || callback.password)) throw new Error('Mobile callback must be a fixed HTTPS URL.');

const config: ExpoConfig = {
  name: 'Fookbase Light', slug: 'fookbase-light', version: '1.0.0',
  scheme: 'fookbase-light', userInterfaceStyle: 'automatic',
  android: { package: process.env.FOOKBASE_ANDROID_PACKAGE ?? 'dev.fookbase.light', versionCode: 1, intentFilters: callback ? [{ action: 'VIEW', autoVerify: true, category: ['BROWSABLE', 'DEFAULT'], data: [{ scheme: 'https', host: callback.host, path: callback.pathname }] }] : [] },
  ios: { bundleIdentifier: process.env.FOOKBASE_IOS_BUNDLE ?? 'dev.fookbase.light', supportsTablet: true },
  plugins: ['expo-router', 'expo-secure-store', 'expo-video', ['expo-image-picker', { photosPermission: 'Cho phép chọn ảnh và video để chia sẻ trên Fookbase Light.', cameraPermission: false, microphonePermission: false }]],
  experiments: { typedRoutes: false },
};
export default config;
