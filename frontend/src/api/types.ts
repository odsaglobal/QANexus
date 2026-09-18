// Shared API types mirroring the backend DTOs.

export interface AuthResult {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  tenantId: string;
  email: string;
  displayName: string;
  systemRole: string;
}

export interface RegisterRequest {
  organizationName: string;
  email: string;
  password: string;
  displayName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export type ProjectStatus = 'Active' | 'Archived' | 'Suspended';

export interface Project {
  id: string;
  name: string;
  key: string;
  description?: string | null;
  status: ProjectStatus;
  environmentCount: number;
  memberCount: number;
  currentUserRole?: 'Owner' | 'Editor' | 'Viewer' | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface CreateProjectRequest {
  name: string;
  key?: string;
  description?: string;
}

export interface UpdateProjectRequest {
  id: string;
  name: string;
  description?: string;
  status: ProjectStatus;
}

export type EnvironmentType = 'Development' | 'Test' | 'Staging' | 'Production' | 'Sandbox';

export interface EnvironmentModel {
  id: string;
  projectId: string;
  name: string;
  type: EnvironmentType;
  baseUrl: string;
  isDefault: boolean;
  createdAtUtc: string;
}

export interface CreateEnvironmentRequest {
  projectId: string;
  name: string;
  type: EnvironmentType;
  baseUrl: string;
  isDefault: boolean;
}

export interface EnvironmentVariable {
  key: string;
  value: string | null;
  type?: 'text' | 'secret';
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

// ----- Requirement Intelligence -----

export type RequirementStatus = 'Uploaded' | 'Analyzing' | 'Analyzed' | 'Failed';

export interface Requirement {
  id: string;
  projectId: string;
  name: string;
  sourceType: string;
  status: RequirementStatus;
  moduleCount: number;
  featureCount: number;
  errorMessage?: string | null;
  analyzedAtUtc?: string | null;
  createdAtUtc: string;
}

export interface UserStory {
  id: string;
  asA: string;
  iWant: string;
  soThat?: string | null;
  acceptanceCriteria: string[];
}

export interface Feature {
  id: string;
  moduleId: string;
  name: string;
  description?: string | null;
  priority: string;
  businessRules: string[];
  scenarioCount: number;
  userStories: UserStory[];
}

export interface RequirementModule {
  id: string;
  name: string;
  description?: string | null;
  features: Feature[];
}

export interface RequirementDetail {
  id: string;
  name: string;
  sourceType: string;
  status: RequirementStatus;
  errorMessage?: string | null;
  textPreview?: string | null;
  content?: string | null;
  modules: RequirementModule[];
}

// ----- Scenarios -----

export type TestPlatform = 'Web' | 'Api' | 'Mobile' | 'Database';

export interface ScenarioStep {
  order: number;
  action: string;
  expectedResult?: string | null;
  needsReview?: boolean;
  reviewReason?: string | null;
  hasRecording?: boolean;
  platform?: TestPlatform;
}

export interface Scenario {
  id: string;
  featureId: string;
  title: string;
  type: string;
  priority: string;
  risk: string;
  source: string;
  preconditions?: string | null;
  expectedResult?: string | null;
  jiraKey?: string | null;
  tags: string[];
  steps: ScenarioStep[];
  proposedSteps?: ScenarioStep[];
  canRevert?: boolean;
  /** When true a Run may use one AI turn to repair a broken locator; when false runs stay fully deterministic. */
  autoHealEnabled?: boolean;
  createdAtUtc: string;
}

export interface JiraIssue {
  key: string;
  summary: string;
  description?: string | null;
  status?: string | null;
  issueType?: string | null;
  priority?: string | null;
  url: string;
}

// ----- Test Data (environment-specific) -----

export type TestDataRow = Record<string, string | null>;

export interface TestDataSet {
  id: string;
  projectId: string;
  environmentId: string;
  name: string;
  description?: string | null;
  columns: string[];
  rows: TestDataRow[];
  rowCount: number;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

// ----- Test Suites -----

export interface TestSuiteScenario {
  scenarioId: string;
  order: number;
  title?: string | null;
}

export interface TestSuite {
  id: string;
  projectId: string;
  name: string;
  description?: string | null;
  scenarioCount: number;
  scenarios: TestSuiteScenario[];
  createdAtUtc: string;
}

// ----- Dashboard -----

export interface DashboardSummary {
  projectCount: number;
  requirementCount: number;
  analyzedRequirementCount: number;
  featureCount: number;
  scenarioCount: number;
  aiScenarioCount: number;
  manualScenarioCount: number;
  testRailScenarioCount: number;
  explorationSessionCount: number;
  discoveredPageCount: number;
}

// ----- AI Insights -----

export type InsightSeverity = 'Critical' | 'Warning' | 'Info';
export type InsightCategory = 'Reliability' | 'Coverage' | 'Quality' | 'Maintenance';

export interface AiInsight {
  id: string;
  category: InsightCategory;
  severity: InsightSeverity;
  title: string;
  summary: string;
  suggestedAction: string;
  /** The observations the finding was derived from, newest first where time-ordered. */
  evidence: string[];
  projectId?: string | null;
  projectName?: string | null;
  scenarioId?: string | null;
  scenarioTitle?: string | null;
  lastSeenUtc?: string | null;
}

export interface AiInsights {
  generatedAtUtc: string;
  runsAnalyzed: number;
  scenariosAnalyzed: number;
  stepResultsAnalyzed: number;
  insights: AiInsight[];
}

// ----- Scenario runs (per-scenario exploration results) -----

export type StepRunStatus = 'Passed' | 'Healed' | 'Failed' | 'Skipped';

/**
 * One assertion compiled from a step's expected result, with its own verdict and the concrete value
 * read off the page. Present only for steps whose expectation was compiled; older runs have none and
 * fall back to rendering the step's `detail` sentence.
 */
export interface StepCheckResult {
  label: string;
  passed: boolean;
  expected?: string | null;
  actual?: string | null;
  /** The assertion could not be evaluated at all (selector matched nothing) — the test needs fixing. */
  unresolved?: boolean;
}

export interface ScenarioStepResult {
  stepOrder: number;
  action: string;
  status: StepRunStatus;
  detail?: string | null;
  url?: string | null;
  checks?: StepCheckResult[] | null;
}

export interface ScenarioRun {
  sessionId: string;
  scenarioId: string;
  status: ExplorationStatus;
  outcome: string;
  passedSteps: number;
  healedSteps: number;
  failedSteps: number;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  steps: ScenarioStepResult[];
}

// ----- Knowledge Graph -----

export interface GraphNode {
  id: string;
  type: string;
  label: string;
  sublabel?: string | null;
}

export interface GraphEdge {
  id: string;
  source: string;
  target: string;
}

export interface KnowledgeGraph {
  nodes: GraphNode[];
  edges: GraphEdge[];
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

// ----- Application Explorer -----

export type ExplorationStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export interface ExplorationSession {
  id: string;
  projectId: string;
  environmentId: string;
  featureId?: string | null;
  scenarioId?: string | null;
  suiteId?: string | null;
  recordedScenarioId?: string | null;
  prompt?: string | null;
  status: ExplorationStatus;
  seedUrl?: string | null;
  maxPages: number;
  maxDepth: number;
  pagesDiscovered: number;
  elementsDiscovered: number;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  errorMessage?: string | null;
  createdAtUtc: string;
}

export interface StartExplorationRequest {
  projectId: string;
  environmentId: string;
  featureId?: string;
  scenarioId?: string;
  prompt?: string;
  seedUrl?: string;
  maxPages?: number;
  maxDepth?: number;
}

export interface DiscoveredPage {
  id: string;
  sessionId: string;
  name: string;
  url: string;
  path: string;
  title?: string | null;
  httpStatusCode?: number | null;
  screenshotPath?: string | null;
  hasAccessibilityTree: boolean;
  depthFromRoot: number;
  discoveredFromUrl?: string | null;
  createdAtUtc: string;
}

/**
 * One persisted step result for a session, tagged with its scenario — the full, ordered execution
 * trace (steps + screenshot + validation detail) behind the Executions "View" detail. Works for both
 * completed and still-running sessions, unlike the live-only SignalR step stream.
 */
export interface SessionStepResult {
  scenarioId: string;
  scenarioTitle: string;
  stepOrder: number;
  action: string;
  status: string;
  detail?: string | null;
  url?: string | null;
  screenshotPath?: string | null;
  createdAtUtc: string;
  checks?: StepCheckResult[] | null;
}

