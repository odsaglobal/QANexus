import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AppSplash } from './components/AppSplash';
import { AppLayout } from './components/AppLayout';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { ProjectsPage } from './pages/ProjectsPage';
import { ProjectDetailPage } from './pages/ProjectDetailPage';
import { ProjectTabRedirect } from './components/ProjectTabRedirect';
import { LocatorsPage } from './pages/LocatorsPage';
import { ExecutionsPage } from './pages/ExecutionsPage';
import { ReportsPage } from './pages/ReportsPage';
import { AgentsPage } from './pages/AgentsPage';
import { SettingsPage } from './pages/SettingsPage';
import { AiInsightsPage } from './pages/AiInsightsPage';
import { AdministrationPage } from './pages/AdministrationPage';
import { ProfilePage } from './pages/ProfilePage';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/login/callback" element={<AuthCallback />} />

      <Route
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route path="/" element={<DashboardPage />} />
        <Route path="/projects" element={<ProjectsPage />} />
        <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
        <Route path="/projects/:projectId/:tab" element={<ProjectDetailPage />} />
        <Route
          path="/requirements"
          element={<ProjectTabRedirect title="Requirement Intelligence" tab="requirements" />}
        />
        <Route
          path="/scenarios"
          element={<ProjectTabRedirect title="Scenario Explorer" tab="scenarios" />}
        />
        <Route path="/explorer" element={<ProjectTabRedirect title="Page Explorer" tab="explorer" />} />
        <Route path="/environments" element={<ProjectTabRedirect title="Environments" tab="environments" />} />
        <Route path="/locators" element={<LocatorsPage />} />
        <Route path="/executions" element={<ExecutionsPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/ai-insights" element={<AiInsightsPage />} />
        <Route path="/agents" element={<AgentsPage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/administration" element={<AdministrationPage />} />
        <Route path="/profile" element={<ProfilePage />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

/** Shown briefly while the Auth0 SDK processes the redirect callback and navigates onward. */
function AuthCallback() {
  return <AppSplash message="Signing you in…" />;
}
