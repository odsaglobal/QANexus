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

export interface ScenarioStep {
  order: number;
  action: string;
  expectedResult?: string | null;
  needsReview?: boolean;
  reviewReason?: string | null;
  hasRecording?: boolean;
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

// ----- Scenario runs (per-scenario exploration results) -----

export type StepRunStatus = 'Passed' | 'Healed' | 'Failed' | 'Skipped';

export interface ScenarioStepResult {
  stepOrder: number;
  action: string;
  status: StepRunStatus;
  detail?: string | null;
  url?: string | null;
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
  elementCount: number;
  depthFromRoot: number;
  discoveredFromUrl?: string | null;
  createdAtUtc: string;
}

export interface ElementLocatorRecord {
  id: string;
  strategy: string;
  value: string;
  isPrimary: boolean;
  confidenceScore: number;
  isVerified: boolean;
  failureCount: number;
}

export interface DiscoveredElement {
  id: string;
  pageId: string;
  name?: string | null;
  role?: string | null;
  ariaLabel?: string | null;
  textContent?: string | null;
  placeholder?: string | null;
  dataTestId?: string | null;
  boundingBoxJson?: string | null;
  screenshotPath?: string | null;
  aiDescription?: string | null;
  isInteractive: boolean;
  isVisible: boolean;
  confidenceScore: number;
  locators: ElementLocatorRecord[];
}
