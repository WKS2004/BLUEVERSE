import { Link, Navigate, Outlet, Route, Routes, useLocation } from 'react-router'
import DashboardPage from '../pages/DashboardPage'
import HomePage from '../pages/HomePage'
import LoginPage from '../pages/LoginPage'
import ProfilePage from '../pages/ProfilePage'
import RegistrationPage from '../pages/RegistrationPage'
import { NotFoundPage, ServerErrorPage } from '../pages/GlobalErrorPage'
import { AdminAccessDenied, AdminIndexPage, AdminPermissionsPage, AdminRolesPage, AdminUsersPage } from '../pages/admin/AdminPages'
import { useAuthSession } from '../features/auth/authSession'
import { authEntryHrefFor } from '../features/auth/authNavigation'
import { hasAnyPermission } from '../features/authorization/permissions'

function RequireAuth() {
  const { status } = useAuthSession()
  const location = useLocation()
  const returnTo = `${location.pathname}${location.search}${location.hash}`

  if (status === 'signed-out') {
    return <Navigate replace to={authEntryHrefFor('/signin', returnTo)} />
  }

  if (status === 'signed-in') return <Outlet />
  if (status === 'checking') return null

  return (
    <main className="flex min-h-[60vh] items-center justify-center bg-coast-paper px-5 py-16 text-coast-ink">
      <section aria-live="polite" className="w-full max-w-lg rounded-3xl border border-coast-line bg-white p-7 text-center shadow-sm sm:p-10">
        <p className="text-xs font-extrabold tracking-[0.15em] text-coast-blue">ACCOUNT ACCESS</p>
        <h1 className="mt-3 font-display text-3xl tracking-[-0.04em]">We couldn’t verify your account.</h1>
        <Link
          className="mt-6 inline-flex min-h-11 items-center justify-center rounded-full bg-coast-deep px-5 text-sm font-extrabold text-white transition hover:bg-coast-blue focus-visible:outline-2 focus-visible:outline-offset-3 focus-visible:outline-coast-blue"
          to={authEntryHrefFor('/signin', returnTo)}
        >
          Continue to sign in
        </Link>
      </section>
    </main>
  )
}

function RequireAnyAdminPermission({ permissions, children }: { permissions: string[]; children: React.ReactNode }) {
  const { user } = useAuthSession()
  return hasAnyPermission(user, permissions) ? children : <AdminAccessDenied />
}

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/signin" element={<LoginPage />} />
      <Route path="/signup" element={<RegistrationPage />} />
      <Route path="/404" element={<NotFoundPage />} />
      <Route path="/500" element={<ServerErrorPage />} />
      <Route element={<RequireAuth />}>
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/admin" element={<RequireAnyAdminPermission permissions={['auth.permission.read', 'auth.role.read', 'auth.user.read']}><AdminIndexPage /></RequireAnyAdminPermission>} />
        <Route path="/admin/permissions" element={<RequireAnyAdminPermission permissions={['auth.permission.read']}><AdminPermissionsPage /></RequireAnyAdminPermission>} />
        <Route path="/admin/roles" element={<RequireAnyAdminPermission permissions={['auth.role.read']}><AdminRolesPage /></RequireAnyAdminPermission>} />
        <Route path="/admin/users" element={<RequireAnyAdminPermission permissions={['auth.user.read']}><AdminUsersPage /></RequireAnyAdminPermission>} />
      </Route>
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
