# Assignment Status and Action Plan (Honest Review)

Last updated: 2026-04-19

This document gives an evidence-based summary of where the project currently stands against the assignment brief, what has already changed, and what should be implemented next. It is written to be transparent for report and presentation use.

## 1) Current Position (Plain-English Summary)

The project now reaches a stronger pass-level baseline. JWT authentication, item management, and rental request flows are implemented and building successfully. The rentals UI now includes incoming owner-side sections plus outgoing request visibility for the requesting user, and approved requests are split into active versus past rentals based on date. A formal repository layer has also been added with a generic `IRepository<T>` contract and concrete repositories for items, rentals, and reviews. The main remaining gaps are testing/coverage evidence, CI/CD workflow evidence, and deeper Tier 2 features such as PostGIS location search and full workflow state depth.

## 2) What Has Been Changed So Far

### Authentication integration

Authentication is connected to backend API endpoints for token issuance and refresh, and authenticated calls are automatically handled through an HTTP interceptor and auth service. This means your app is no longer relying on purely local login behavior for the main flow, and token lifecycle handling is in place in a production-like way.

### Item management

Item create/list/detail/update-owner-only flows are implemented end-to-end. This covers a major part of the Tier 1 requirements and shows that your MAUI app, API endpoints, and database model are connected coherently for CRUD-style behavior with ownership checks.

### Rental request flow

The project supports creating rental requests and owner moderation (approve/deny) for incoming requests. The core request lifecycle exists at a basic level and demonstrates key user interactions for a rental marketplace.

### Past rentals split (latest change)

The rentals UI and view model were updated so approved rentals are split into Active Rentals and Past Rentals using date logic. Rentals with an end date in the past are no longer mixed into active items. This improves correctness of the owner-side rentals view and aligns better with real-world expectations.

### Outgoing requests in app UI

The rentals page now surfaces outgoing rental requests in addition to the incoming owner-side sections. This closes the previously identified Tier 1 gap where outgoing requests existed in the API but were not visible to users in the MAUI rentals screen.

### Repository pattern implementation

The data layer now includes a reusable generic repository abstraction plus concrete repositories for Items, Rentals, and Reviews. Repository interfaces and implementations are wired into dependency injection in the API project, and a Review model is now present in the database domain layer to support the required review repository structure.

## 3) Requirement-by-Requirement Status

### Tier 1 (Pass-level core)

#### User authentication integration

Status: Mostly complete. JWT issue, storage, bearer attachment, and refresh handling are implemented. This is one of your strongest completed areas.

#### Item management

Status: Complete for the listed basics. Create, browse, detail view, and owner-only updates are present and functioning.

#### Basic rental request

Status: Complete for pass-level scope. Submitting requests is implemented, incoming owner-side request handling is implemented, and outgoing requests are now visible in the MAUI rentals UI.

#### MVVM architecture

Status: Substantially implemented, not fully complete. You have ViewModels, observable properties, and command-based interactions in place. There is still remaining cleanup and consistency work to claim full separation across all modules.

#### Repository pattern

Status: Partially complete with key pass-level artifacts delivered. `IRepository<T>` and concrete repositories for Items, Rentals, and Reviews are now implemented and registered in DI. Further refactoring can still improve how broadly these repositories are consumed across all endpoint paths.

### Tier 2 (Merit-level)

#### Location-based discovery (PostGIS)

Status: Not implemented to brief. Latitude/longitude fields exist, but there is no PostGIS geography point storage or ST_DWithin/ST_MakePoint near-me query path implemented.

#### Rental workflow management depth

Status: Partially implemented. Requested to Approved/Denied exists, but Out for Rent, Returned, Completed, and Overdue transitions are not fully modeled as required.

#### Reviews and feedback

Status: Not implemented. Review entities, rating/comment submission flow, and profile aggregation are missing.

#### Service layer completeness

Status: Partially implemented. Some service abstractions exist, but dedicated LocationService, RentalService (business rules), and ReviewService with full assignment-level rules are not complete.

#### Comprehensive testing

Status: Not implemented to required level. There is no visible xUnit test project, no coverage report, and no documented percentage against thresholds.

### Tier 3 (Distinction bonus)

#### State pattern

Status: Not implemented. No dedicated rental state classes or state machine pattern currently present.

#### MediatR/CQRS-lite

Status: Not implemented. No command/event handler structure for rental workflow orchestration is currently present.

#### Advanced quality extras

Status: Not implemented. No SonarCloud configuration or advanced coverage reporting pipeline is currently present.

## 4) DevOps and Process Status

### Build health

The solution builds successfully in the current environment, which is a good baseline signal. There are still a high number of warnings, so technical quality polish remains a meaningful task.

### CI/CD

No GitHub Actions workflow is currently present in this repository, so automated build/test evidence is missing for LO2 expectations.

### Source control maturity

Current commit count is below the assignment recommendation threshold of 15+ commits over the project period. You should continue with focused, meaningful commits as features are completed.

### AI transparency (LO4)

There is currently no dedicated AI-usage section documenting what tools were used, where generated output was validated, and how you controlled correctness. This should be added before submission.

## 5) What We Will Do Next (Implementation Plan)

### Step 1: Stabilize and evidence pass-level work

Tier 1 risk has been reduced by implementing outgoing request UI coverage and introducing the required repository pattern artifacts. The next priority is to stabilize these changes and present clear evidence in report and demo materials.

### Step 2: Add testing foundation and measurable coverage

Create a dedicated xUnit test project, add unit tests for key ViewModels and services, then produce coverage metrics. This is essential for both quality outcomes and CI/CD value.

### Step 3: Add CI workflow for build + test

Introduce a GitHub Actions workflow that runs restore/build/test on push and pull requests. This provides hard evidence of process quality and directly supports LO2.

### Step 4: Implement one strong Tier 2 vertical slice

The highest-impact Tier 2 candidate is rental workflow hardening: add overlap validation, strengthen status transitions, and enforce business rules in services. This adds depth without requiring every bonus feature at once.

### Step 5: Add AI usage documentation

Create a concise section in project documentation describing AI tools used, prompts/patterns used, validation approach, and examples of manual verification. This protects LO4 marks and supports presentation defense.

## 6) Honest Grade Position Right Now

Based on current code and documentation evidence in this repository, the project appears to be around partial Tier 1 completion with good momentum but clear missing assessment artifacts. In practical terms, this is likely near pass-borderline territory until testing, CI/CD, repository pattern, and at least one deeper Tier 2 area are completed and evidenced.

## 7) Recommended Submission Narrative (If Presenting Today)

If you had to present now, position the work as a stable and functioning core architecture with strong API-auth integration and completed item/rental basics, while being explicit that advanced workflow, quality automation, and coverage work are actively in progress. This framing is honest, defensible, and better received than overstating completeness.
