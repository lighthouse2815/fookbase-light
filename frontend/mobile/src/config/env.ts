export function validateApiBaseUrl(value: string | undefined): string {
  if (!value) throw new Error('Chưa cấu hình EXPO_PUBLIC_API_BASE_URL cho ứng dụng.');
  const url = new URL(value);
  if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash ||
      (url.pathname !== '/' && url.pathname !== '') ||
      ['localhost', '127.0.0.1', '[::1]', '0.0.0.0'].includes(url.hostname)) {
    throw new Error('API phải là địa chỉ HTTPS công khai, không chứa đường dẫn hoặc thông tin đăng nhập.');
  }
  return url.origin;
}

export function getApiBaseUrl() {
  return validateApiBaseUrl(process.env.EXPO_PUBLIC_API_BASE_URL);
}
