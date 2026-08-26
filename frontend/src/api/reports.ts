import { apiClient } from '../lib/apiClient';

export interface ScenarioTypeSlice {
  type: string;
  count: number;
}

export interface WeeklyTrendPoint {
  week: string;
  passed: number;
  failed: number;
}

export interface FailingScenario {
  title: string;
  failedSteps: number;
}

export interface RecentRun {
  sessionId: string;
  label: string;
  projectName: string;
  status: string;
  startedAtUtc?: string | null;
  durationSeconds?: number | null;
}

export interface ReportsSummary {
  totalRuns: number;
  completedRuns: number;
  failedRuns: number;
  runningRuns: number;
  stepsPassed: number;
  stepsHealed: number;
  stepsFailed: number;
  stepPassRate: number;
  scenarioMix: ScenarioTypeSlice[];
  weeklyTrend: WeeklyTrendPoint[];
  topFailingScenarios: FailingScenario[];
  recentRuns: RecentRun[];
}

export async function getReportsSummary(): Promise<ReportsSummary> {
  const { data } = await apiClient.get<ReportsSummary>('/reports/summary');
  return data;
}
