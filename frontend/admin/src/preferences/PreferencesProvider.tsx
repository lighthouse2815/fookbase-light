import { useEffect, useMemo, useState } from 'react'
import { PreferencesContext } from './context'
import type { AdminLanguage, AdminTheme, PreferencesContextValue } from './context'

const storageKey = 'fookbase.admin.preferences'

const messages = {
  en: {
    switchToLight: 'Switch to light mode', switchToDark: 'Switch to dark mode', language: 'Language',
    adminCenter: 'Admin Center', greeting: 'Hello', refresh: 'Refresh', signOut: 'Sign out',
    overview: 'Overview', reports: 'Reports', users: 'Accounts', totalAccounts: 'Total accounts',
    activeAccounts: 'Active accounts', visiblePosts: 'Visible posts', pendingReports: 'Pending reports',
    moderationPriority: 'Moderation priority', pendingSummary: 'reports are waiting for a decision.', openQueue: 'Open queue',
    reportQueue: 'Report queue', results: 'results', all: 'All', pending: 'Pending', reviewed: 'Reviewed',
    resolved: 'Resolved', dismissed: 'Dismissed', noReports: 'No reports found.', post: 'Post', user: 'User',
    reason: 'Reason', target: 'Target', reporter: 'Reporter', markReviewed: 'Mark reviewed', removePost: 'Remove post',
    dismiss: 'Dismiss', resolve: 'Resolve', loadMore: 'Load more', search: 'Search', searchAccounts: 'Email or username',
    noAccounts: 'No accounts found.', active: 'Active', disabled: 'Disabled', joined: 'Joined', disable: 'Disable',
    enable: 'Enable', removePostConfirm: 'Remove this post? This action cannot be undone.', accountConfirm: 'this account?',
    unableLoad: 'Unable to load admin data.', unableReports: 'Unable to load reports.', unableUpdateReport: 'Unable to update the report.',
    unableRemovePost: 'Unable to remove the post.', unableUpdateAccount: 'Unable to update the account.', unableSearch: 'Unable to search accounts.',
    adminSignIn: 'ADMIN SIGN IN', welcomeBack: 'Welcome back', adminRoleHint: 'Use an account with the Admin role.',
    email: 'Email', password: 'Password', yourPassword: 'Your password', signIn: 'Sign in to Admin Center', signingIn: 'Signing in...',
    protectedWorkspace: 'Manage reports, content and account safety from one protected workspace.', restricted: 'Restricted to authorised administrators.',
    adminRequired: 'This account does not have administrator access.', unableSignIn: 'Unable to sign in.',
    newReports: 'new reports', viewPendingReports: 'View pending reports', retry: 'Retry', loadingReports: 'Loading reports...', loadingAccounts: 'Loading accounts...',
  },
  vi: {
    switchToLight: 'Chuyển sang giao diện sáng', switchToDark: 'Chuyển sang giao diện tối', language: 'Ngôn ngữ',
    adminCenter: 'Trung tâm quản trị', greeting: 'Xin chào', refresh: 'Làm mới', signOut: 'Đăng xuất',
    overview: 'Tổng quan', reports: 'Báo cáo', users: 'Tài khoản', totalAccounts: 'Tổng tài khoản',
    activeAccounts: 'Đang hoạt động', visiblePosts: 'Bài viết hiển thị', pendingReports: 'Báo cáo chờ xử lý',
    moderationPriority: 'Ưu tiên kiểm duyệt', pendingSummary: 'báo cáo đang chờ quyết định.', openQueue: 'Mở hàng đợi',
    reportQueue: 'Hàng đợi báo cáo', results: 'kết quả', all: 'Tất cả', pending: 'Chờ xử lý', reviewed: 'Đã xem',
    resolved: 'Đã xử lý', dismissed: 'Đã bỏ qua', noReports: 'Không có báo cáo nào.', post: 'Bài viết', user: 'Người dùng',
    reason: 'Lý do', target: 'Mục tiêu', reporter: 'Người báo cáo', markReviewed: 'Đã xem', removePost: 'Gỡ bài',
    dismiss: 'Bỏ qua', resolve: 'Đã xử lý', loadMore: 'Tải thêm', search: 'Tìm', searchAccounts: 'Email hoặc username',
    noAccounts: 'Không tìm thấy tài khoản.', active: 'Đang hoạt động', disabled: 'Đã vô hiệu hóa', joined: 'Tham gia', disable: 'Vô hiệu hóa',
    enable: 'Kích hoạt', removePostConfirm: 'Gỡ bài viết này? Thao tác không thể hoàn tác.', accountConfirm: 'tài khoản này?',
    unableLoad: 'Không thể tải dữ liệu quản trị.', unableReports: 'Không thể tải báo cáo.', unableUpdateReport: 'Không thể cập nhật báo cáo.',
    unableRemovePost: 'Không thể gỡ bài viết.', unableUpdateAccount: 'Không thể cập nhật tài khoản.', unableSearch: 'Không thể tìm tài khoản.',
    adminSignIn: 'ĐĂNG NHẬP QUẢN TRỊ', welcomeBack: 'Chào mừng trở lại', adminRoleHint: 'Hãy dùng tài khoản có role Admin.',
    email: 'Email', password: 'Mật khẩu', yourPassword: 'Mật khẩu của bạn', signIn: 'Đăng nhập Admin Center', signingIn: 'Đang đăng nhập...',
    protectedWorkspace: 'Quản lý báo cáo, nội dung và an toàn tài khoản trong một không gian bảo vệ.', restricted: 'Chỉ quản trị viên được ủy quyền mới có quyền truy cập.',
    adminRequired: 'Tài khoản này không có quyền quản trị.', unableSignIn: 'Không thể đăng nhập.',
    newReports: 'báo cáo mới', viewPendingReports: 'Xem báo cáo chờ xử lý', retry: 'Thử lại', loadingReports: 'Đang tải báo cáo...', loadingAccounts: 'Đang tải tài khoản...',
  },
} as const

function getInitialPreferences(): { language: AdminLanguage; theme: AdminTheme } {
  try {
    const stored = JSON.parse(localStorage.getItem(storageKey) ?? '{}') as { language?: AdminLanguage; theme?: AdminTheme }
    const language: AdminLanguage = stored.language === 'en' || stored.language === 'vi'
      ? stored.language
      : navigator.language.startsWith('vi') ? 'vi' : 'en'
    const theme: AdminTheme = stored.theme === 'light' || stored.theme === 'dark' ? stored.theme : 'dark'
    return {
      language,
      theme,
    }
  } catch {
    return { language: navigator.language.startsWith('vi') ? 'vi' : 'en', theme: 'dark' }
  }
}

export function PreferencesProvider({ children }: { children: React.ReactNode }) {
  const [{ language, theme }, setPreferences] = useState(getInitialPreferences)

  useEffect(() => {
    document.documentElement.lang = language
    document.documentElement.dataset.theme = theme
    localStorage.setItem(storageKey, JSON.stringify({ language, theme }))
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
