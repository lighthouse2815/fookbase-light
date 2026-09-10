import { useEffect, useMemo, useState } from 'react'
import { PreferencesContext } from './context'
import type { AppLanguage, AppTheme, PreferencesContextValue } from './context'

const preferencesStorageKey = 'fookbase.preferences'

const messages = {
  en: {
    home: 'Home',
    feed: 'Feed',
    explore: 'Explore',
    messages: 'Messages',
    games: 'Games',
    profile: 'Profile',
    searchFookbase: 'Search Fookbase',
    signOut: 'Sign out',
    notifications: 'Notifications',
    messageNotifications: 'Message notifications',
    openMessages: 'Open messages',
    allCaughtUp: 'You are all caught up.',
    newFriendRequest: 'New friend request',
    friendRequestAccepted: 'Friend request accepted',
    viewProfile: 'View profile',
    newMessage: 'New message',
    switchToLight: 'Switch to light mode',
    switchToDark: 'Switch to dark mode',
    language: 'Language',
    appearance: 'Appearance',
    english: 'English',
    vietnamese: 'Tiếng Việt',
    socialSpace: 'YOUR SOCIAL SPACE',
    quieterCorner: 'A quieter corner of the internet.',
    socialDescription: 'Keep up with your people, share what matters, and turn everyday moments into conversations.',
    shareWorld: 'Share your world',
    shareWorldDescription: 'Posts, reactions, and conversations that feel close.',
    stayConnected: 'Stay connected',
    stayConnectedDescription: 'Private messages and live updates with your friends.',
    madeForCircle: 'Made for your circle',
    madeForCircleDescription: 'Your feed is shaped around the people you know.',
    socialSimplified: 'SOCIAL, SIMPLIFIED',
    emailVerification: 'EMAIL VERIFICATION',
    resetPassword: 'RESET PASSWORD',
    accountRecovery: 'ACCOUNT RECOVERY',
    joinFookbase: 'JOIN FOOKBASE',
    welcomeBack: 'WELCOME BACK',
    verifyEmailTitle: 'Verify your email.',
    newPasswordTitle: 'Choose a new password.',
    resetPasswordTitle: 'Reset your password.',
    createSpaceTitle: 'Create your space.',
    signInSpaceTitle: 'Sign in to your space.',
    verifyEmailDescription: 'We are confirming the email address linked to your Fookbase account.',
    newPasswordDescription: 'Use a strong password you do not use elsewhere.',
    resetPasswordDescription: 'Enter your email and we will send a secure reset link.',
    createSpaceDescription: 'Set up your account in a moment and start connecting.',
    signInSpaceDescription: 'Enter your details to continue where you left off.',
    emailAddress: 'Email address',
    username: 'Username',
    chooseUsername: 'Choose a username',
    password: 'Password',
    passwordAtLeastEight: 'At least 8 characters',
    yourPassword: 'Your password',
    showPassword: 'Show password',
    hidePassword: 'Hide password',
    show: 'SHOW',
    hide: 'HIDE',
    confirmPassword: 'Confirm new password',
    repeatPassword: 'Repeat your new password',
    respectfulUse: 'By creating an account, you agree to use Fookbase respectfully and keep your login details private.',
    pleaseWait: 'Please wait...',
    sendResetLink: 'Send reset link',
    resetPasswordAction: 'Reset password',
    createAccount: 'Create account',
    signIn: 'Sign in',
    backToSignIn: 'Back to sign in',
    forgotPassword: 'Forgot password?',
    or: 'OR',
    alreadyHaveAccount: 'I already have an account',
    createNewAccount: 'Create a new account',
    invalidVerificationLink: 'The verification link is invalid.',
    verifyingEmail: 'Verifying your email...',
    emailVerified: 'Your email has been verified. You can continue using Fookbase.',
    unableVerifyEmail: 'Unable to verify email from this link.',
    unableVerify: 'Unable to verify email.',
    unableAuthenticate: 'Unable to authenticate.',
    resetLinkSent: 'If this email has an account, we sent a password-reset link.',
    passwordReset: 'Your password has been reset. You can sign in with the new password.',
  },
  vi: {
    home: 'Trang chủ',
    feed: 'Bảng tin',
    explore: 'Khám phá',
    messages: 'Tin nhắn',
    games: 'Trò chơi',
    profile: 'Trang cá nhân',
    searchFookbase: 'Tìm kiếm Fookbase',
    signOut: 'Đăng xuất',
    notifications: 'Thông báo',
    messageNotifications: 'Thông báo tin nhắn',
    openMessages: 'Mở tin nhắn',
    allCaughtUp: 'Bạn đã xem hết thông báo.',
    newFriendRequest: 'Lời mời kết bạn mới',
    friendRequestAccepted: 'Đã chấp nhận lời mời kết bạn',
    viewProfile: 'Xem trang cá nhân',
    newMessage: 'Tin nhắn mới',
    switchToLight: 'Chuyển sang giao diện sáng',
    switchToDark: 'Chuyển sang giao diện tối',
    language: 'Ngôn ngữ',
    appearance: 'Giao diện',
    english: 'English',
    vietnamese: 'Tiếng Việt',
    socialSpace: 'KHÔNG GIAN XÃ HỘI CỦA BẠN',
    quieterCorner: 'Một góc internet yên tĩnh hơn.',
    socialDescription: 'Kết nối với những người quan trọng, chia sẻ điều có ý nghĩa và biến khoảnh khắc thường ngày thành cuộc trò chuyện.',
    shareWorld: 'Chia sẻ thế giới của bạn',
    shareWorldDescription: 'Bài viết, cảm xúc và trò chuyện gần gũi.',
    stayConnected: 'Luôn kết nối',
    stayConnectedDescription: 'Tin nhắn riêng tư và cập nhật trực tiếp với bạn bè.',
    madeForCircle: 'Dành cho vòng kết nối của bạn',
    madeForCircleDescription: 'Bảng tin được tạo nên từ những người bạn biết.',
    socialSimplified: 'MẠNG XÃ HỘI, ĐƠN GIẢN HƠN',
    emailVerification: 'XÁC MINH EMAIL',
    resetPassword: 'ĐẶT LẠI MẬT KHẨU',
    accountRecovery: 'KHÔI PHỤC TÀI KHOẢN',
    joinFookbase: 'THAM GIA FOOKBASE',
    welcomeBack: 'CHÀO MỪNG TRỞ LẠI',
    verifyEmailTitle: 'Xác minh email của bạn.',
    newPasswordTitle: 'Chọn mật khẩu mới.',
    resetPasswordTitle: 'Đặt lại mật khẩu.',
    createSpaceTitle: 'Tạo không gian của bạn.',
    signInSpaceTitle: 'Đăng nhập vào không gian của bạn.',
    verifyEmailDescription: 'Chúng tôi đang xác nhận địa chỉ email được liên kết với tài khoản Fookbase của bạn.',
    newPasswordDescription: 'Hãy dùng mật khẩu mạnh mà bạn không sử dụng ở nơi khác.',
    resetPasswordDescription: 'Nhập email và chúng tôi sẽ gửi liên kết đặt lại an toàn.',
    createSpaceDescription: 'Thiết lập tài khoản trong giây lát và bắt đầu kết nối.',
    signInSpaceDescription: 'Nhập thông tin để tiếp tục từ nơi bạn đã dừng lại.',
    emailAddress: 'Địa chỉ email',
    username: 'Tên người dùng',
    chooseUsername: 'Chọn tên người dùng',
    password: 'Mật khẩu',
    passwordAtLeastEight: 'Ít nhất 8 ký tự',
    yourPassword: 'Mật khẩu của bạn',
    showPassword: 'Hiện mật khẩu',
    hidePassword: 'Ẩn mật khẩu',
    show: 'HIỆN',
    hide: 'ẨN',
    confirmPassword: 'Xác nhận mật khẩu mới',
    repeatPassword: 'Nhập lại mật khẩu mới',
    respectfulUse: 'Khi tạo tài khoản, bạn đồng ý sử dụng Fookbase một cách tôn trọng và bảo mật thông tin đăng nhập.',
    pleaseWait: 'Vui lòng chờ...',
    sendResetLink: 'Gửi liên kết đặt lại',
    resetPasswordAction: 'Đặt lại mật khẩu',
    createAccount: 'Tạo tài khoản',
    signIn: 'Đăng nhập',
    backToSignIn: 'Quay lại đăng nhập',
    forgotPassword: 'Quên mật khẩu?',
    or: 'HOẶC',
    alreadyHaveAccount: 'Tôi đã có tài khoản',
    createNewAccount: 'Tạo tài khoản mới',
    invalidVerificationLink: 'Liên kết xác minh không hợp lệ.',
    verifyingEmail: 'Đang xác minh email của bạn...',
    emailVerified: 'Email đã được xác minh. Bạn có thể tiếp tục sử dụng Fookbase.',
    unableVerifyEmail: 'Không thể xác minh email từ liên kết này.',
    unableVerify: 'Không thể xác minh email.',
    unableAuthenticate: 'Không thể xác thực.',
    resetLinkSent: 'Nếu email này có tài khoản, chúng tôi đã gửi liên kết đặt lại mật khẩu.',
    passwordReset: 'Mật khẩu đã được đặt lại. Bạn có thể đăng nhập bằng mật khẩu mới.',
  },
} as const

interface StoredPreferences {
  language?: AppLanguage
  theme?: AppTheme
}

function getInitialPreferences(): Required<StoredPreferences> {
  try {
    const stored = JSON.parse(localStorage.getItem(preferencesStorageKey) ?? '{}') as StoredPreferences
    const language = stored.language === 'en' || stored.language === 'vi'
      ? stored.language
      : navigator.language.toLowerCase().startsWith('vi') ? 'vi' : 'en'
    const theme = stored.theme === 'light' || stored.theme === 'dark' ? stored.theme : 'dark'
    return { language, theme }
  } catch {
    return { language: navigator.language.toLowerCase().startsWith('vi') ? 'vi' : 'en', theme: 'dark' }
  }
}

export function PreferencesProvider({ children }: { children: React.ReactNode }) {
  const [{ language, theme }, setPreferences] = useState(getInitialPreferences)

  useEffect(() => {
    document.documentElement.lang = language
    document.documentElement.dataset.theme = theme
    localStorage.setItem(preferencesStorageKey, JSON.stringify({ language, theme }))
  }, [language, theme])

  const value = useMemo<PreferencesContextValue>(() => ({
    language,
    theme,
    setLanguage: (nextLanguage) => setPreferences((current) => ({ ...current, language: nextLanguage })),
    setTheme: (nextTheme) => setPreferences((current) => ({ ...current, theme: nextTheme })),
    t: (key) => messages[language][key as keyof typeof messages.en],
  }), [language, theme])

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>
}
