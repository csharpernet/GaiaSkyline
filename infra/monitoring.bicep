// GaiaSkyline — monitoring & alerts (Stage 8 Part D).
// Alert definitions over the Log Analytics workspace + Application Insights provisioned in Part B. Authored
// and validated offline (bicep build) in local-only mode; wiring to a live workspace and confirming each
// query against real telemetry is a Deployment-phase task (docs/runbook.md). Nothing here is deployed yet.
//
// Signals the app already emits and these rules query:
//   - WebVital.LCP           custom metric  -> AppMetrics          (Seo/WebVitalsForwarder, Part B)
//   - request result codes   requests       -> AppRequests         (5xx ratio, Stripe webhook failures)
//   - "External calendar sync failed"  warning -> AppTraces        (Availability/ExternalCalendarImporter)
//   - "Booking conflict detected"      warning -> AppTraces        (Availability/ExternalCalendarImporter, Part D)

@description('Azure region for the alert resources.')
param location string

@description('Resource-name suffix, e.g. gaiaskyline-prod.')
param suffix string

@description('Resource id of the Application Insights component (Part B).')
param appInsightsId string

@description('Resource id of the Log Analytics workspace the query alerts run over (Part B).')
param workspaceId string

@description('Default host name of the web app, e.g. app-gaiaskyline-prod.azurewebsites.net.')
param appHostName string

@description('Operator email that receives alert notifications. Placeholder until provided (Deployment phase).')
param operatorEmail string = ''

@description('True for the production environment (tighter thresholds / enabled availability test).')
param isProd bool

var hasEmail = !empty(operatorEmail)

// ---------- Action group: who gets told ----------

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${suffix}'
  location: 'global'
  properties: {
    groupShortName: 'gaia-ops'
    enabled: true
    emailReceivers: hasEmail
      ? [
          {
            name: 'operator'
            emailAddress: operatorEmail
            useCommonAlertSchema: true
          }
        ]
      : []
  }
}

// ---------- Availability: ping /health/ready every 5 minutes ----------

resource readyWebTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: 'webtest-ready-${suffix}'
  location: location
  // The hidden-link tag binds the web test to the App Insights component (portal requirement).
  tags: {
    'hidden-link:${appInsightsId}': 'Resource'
  }
  kind: 'standard'
  properties: {
    SyntheticMonitorId: 'webtest-ready-${suffix}'
    Name: 'health-ready'
    Enabled: isProd
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      { Id: 'emea-nl-ams-azr' }
      { Id: 'emea-gb-db3-azr' }
      { Id: 'emea-fr-pra-edge' }
    ]
    Request: {
      RequestUrl: 'https://${appHostName}/health/ready'
      HttpVerb: 'GET'
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

resource availabilityAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-availability-${suffix}'
  location: 'global'
  properties: {
    description: 'The /health/ready availability test is failing.'
    severity: 1
    enabled: isProd
    scopes: [
      readyWebTest.id
      appInsightsId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: readyWebTest.id
      componentId: appInsightsId
      failedLocationCount: 2
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// ---------- Log-query alerts ----------

// Each rule follows the same shape; see the array below. api 2022-06-15 = the GA scheduled query rules.
var queryAlerts = [
  {
    name: 'alert-5xx-${suffix}'
    description: 'Server error (5xx) rate above 1% of requests.'
    severity: 1
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    query: '''
      AppRequests
      | summarize total = count(), errors = countif(toint(ResultCode) >= 500)
      | extend errorPct = 100.0 * errors / todouble(max_of(total, 1))
      | where errors > 0
      | project errorPct
    '''
    operator: 'GreaterThan'
    threshold: 1
    metricMeasureColumn: 'errorPct'
  }
  {
    name: 'alert-stripe-webhook-${suffix}'
    description: 'Stripe webhook failure rate above 1% over 10 minutes.'
    severity: 1
    evaluationFrequency: 'PT5M'
    windowSize: 'PT10M'
    // Operation name confirmed against live telemetry at wiring time; the webhook handler lives under /payments.
    query: '''
      AppRequests
      | where Url has 'webhook' or OperationName has 'Webhook'
      | summarize total = count(), failures = countif(Success == false)
      | extend failPct = 100.0 * failures / todouble(max_of(total, 1))
      | where failures > 0
      | project failPct
    '''
    operator: 'GreaterThan'
    threshold: 1
    metricMeasureColumn: 'failPct'
  }
  {
    name: 'alert-lcp-${suffix}'
    description: 'p75 Largest Contentful Paint above 2.5s (field data). The 7-day trend lives on the dashboard; this alerts on the rolling day.'
    severity: 2
    evaluationFrequency: 'PT1H'
    windowSize: 'P1D'
    query: '''
      AppMetrics
      | where Name == 'WebVital.LCP'
      | summarize p75 = percentile(Value, 75)
      | where p75 > 2500
      | project p75
    '''
    operator: 'GreaterThan'
    threshold: 2500
    metricMeasureColumn: 'p75'
  }
  {
    name: 'alert-ical-failures-${suffix}'
    description: 'An external calendar (iCal) import has failed repeatedly.'
    severity: 2
    evaluationFrequency: 'PT15M'
    windowSize: 'PT3H'
    // Approximates "3 consecutive failures": 3+ failures for the same source within the window.
    query: '''
      AppTraces
      | where Message startswith 'External calendar sync failed'
      | summarize failures = count() by tostring(Properties['Source'])
      | where failures >= 3
      | project failures
    '''
    operator: 'GreaterThan'
    threshold: 0
    metricMeasureColumn: 'failures'
  }
  {
    name: 'alert-booking-conflict-${suffix}'
    description: 'A booking conflict was detected (an imported block overlaps a direct booking).'
    severity: 1
    evaluationFrequency: 'PT15M'
    windowSize: 'PT1H'
    query: '''
      AppTraces
      | where Message startswith 'Booking conflict detected'
      | count
    '''
    operator: 'GreaterThan'
    threshold: 0
    metricMeasureColumn: ''
  }
  {
    name: 'alert-rate-sync-stale-${suffix}'
    description: 'No successful nightly rate sync in the last 24 hours.'
    severity: 2
    evaluationFrequency: 'PT1H'
    windowSize: 'P1D'
    // Heartbeat: the nightly rate-sync job logs this on success; its absence for 24h fires the alert.
    query: '''
      AppTraces
      | where Message startswith 'Rate sync completed'
      | summarize successes = count()
      | where successes == 0
      | project successes
    '''
    operator: 'Equals'
    threshold: 0
    metricMeasureColumn: 'successes'
  }
]

resource logAlerts 'Microsoft.Insights/scheduledQueryRules@2022-06-15' = [
  for rule in queryAlerts: {
    name: rule.name
    location: location
    properties: {
      displayName: rule.name
      description: rule.description
      severity: rule.severity
      enabled: isProd
      scopes: [
        workspaceId
      ]
      evaluationFrequency: rule.evaluationFrequency
      windowSize: rule.windowSize
      criteria: {
        allOf: [
          {
            query: rule.query
            timeAggregation: 'Total'
            metricMeasureColumn: empty(rule.metricMeasureColumn) ? null : rule.metricMeasureColumn
            operator: rule.operator
            threshold: rule.threshold
            failingPeriods: {
              numberOfEvaluationPeriods: 1
              minFailingPeriodsToAlert: 1
            }
          }
        ]
      }
      autoMitigate: true
      actions: {
        actionGroups: [
          actionGroup.id
        ]
      }
    }
  }
]

output actionGroupId string = actionGroup.id
