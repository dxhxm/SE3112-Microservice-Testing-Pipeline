import { expect } from 'chai';
import { WebDriver } from 'selenium-webdriver';
import { createWebDriver } from '../config/selenium.config';
import { SignupPage } from '../pages/SignupPage';
import { LoginPage } from '../pages/LoginPage';
import { TournamentsPage } from '../pages/TournamentsPage';
import { ProfileSettingsPage } from '../pages/ProfileSettingsPage';
import { TestLogger } from '../utils/TestLogger';
import { ApiClient } from '../utils/ApiClient';

describe('TC-E2E-01: Multi-Service User Journey & Cross-Service State Propagation', function () {
  this.timeout(90000);

  let driver: WebDriver;
  let signupPage: SignupPage;
  let loginPage: LoginPage;
  let tournamentsPage: TournamentsPage;
  let profilePage: ProfileSettingsPage;

  const timestamp = Date.now();
  const testUser = `e2e_user_${timestamp}`;
  const testEmail = `e2e_${timestamp}@arena.test`;
  const testPassword = 'Password123!';
  const tournamentName = `E2E Championship ${timestamp}`;
  const seasonId = `S-E2E-${timestamp.toString().slice(-4)}`;

  let authToken: string = '';

  before(async function () {
    TestLogger.info('Initializing Selenium Chrome WebDriver...');
    driver = await createWebDriver();
    signupPage = new SignupPage(driver);
    loginPage = new LoginPage(driver);
    tournamentsPage = new TournamentsPage(driver);
    profilePage = new ProfileSettingsPage(driver);
  });

  after(async function () {
    // Automated Teardown logic
    if (authToken) {
      TestLogger.teardown(`Purging test account [${testUser}] via User Service API...`);
      try {
        const status = await ApiClient.deleteAccount(authToken);
        TestLogger.teardown(`Account deletion completed with status ${status}. No dirty DB state left.`);
      } catch (err: any) {
        TestLogger.warn(`Teardown warning: ${err.message}`);
      }
    }

    if (driver) {
      TestLogger.info('Terminating WebDriver session.');
      await driver.quit();
    }
  });

  it('Step 1: Should register a new user in User Service via UI form', async function () {
    TestLogger.step(1, 'UI -> User Service (:5168)', `Registering username "${testUser}" at /signup`);
    await signupPage.open();
    await signupPage.signup(testUser, testEmail, testPassword);

    await loginPage.waitForUrlContains('/login', 15000);
    const currentUrl = await loginPage.getCurrentUrl();
    expect(currentUrl).to.include('/login');
    TestLogger.success('Registration successful. Redirected to /login.');
  });

  it('Step 2: Should authenticate the user, receive JWT, and load dashboard', async function () {
    TestLogger.step(2, 'UI -> User Service (:5168)', `Authenticating user "${testUser}" and obtaining JWT`);
    await loginPage.open();
    await loginPage.login(testUser, testPassword);

    await loginPage.waitForUrlContains('/', 15000);
    TestLogger.success('Authenticated successfully. Browser session established.');

    // Fetch token for direct API validations and teardown
    const loginRes = await ApiClient.login(testUser, testPassword);
    authToken = loginRes.token;
    expect(authToken).to.be.a('string').and.not.be.empty;
    TestLogger.info(`Acquired JWT token: ${authToken.substring(0, 16)}...`);
  });

  it('Step 3: Should switch role to Organizer via Profile Settings', async function () {
    TestLogger.step(3, 'UI -> User Service (:5168)', 'Upgrading user role from Viewer to Organizer');
    await profilePage.open();
    const initialBtnText = await profilePage.getRoleButtonText();
    expect(initialBtnText).to.include('Upgrade to Organizer');

    await profilePage.toggleRole();
    await driver.sleep(1500); // Allow React state and JWT claims to synchronize

    TestLogger.success('User role successfully updated to Organizer.');
  });

  it('Step 4: Should navigate to Tournaments and display "Create Tournament" button', async function () {
    TestLogger.step(4, 'UI -> Tournament Service (:8082)', 'Navigating to /tournaments as Organizer');
    await tournamentsPage.open();
    const canCreate = await tournamentsPage.isCreateButtonVisible();
    expect(canCreate).to.be.true;
    TestLogger.success('"Create Tournament" action button is visible for Organizer role.');
  });

  it('Step 5: Should create tournament and assert cross-service rendering in UI', async function () {
    TestLogger.step(5, 'UI -> Tournament Service (:8082)', `Creating tournament "${tournamentName}"`);
    await tournamentsPage.openCreateModal();
    await tournamentsPage.createTournament(tournamentName, seasonId);

    const isCreated = await tournamentsPage.isTournamentPresent(tournamentName);
    expect(isCreated).to.be.true;
    TestLogger.success(`Tournament "${tournamentName}" successfully persisted and rendered in UI.`);
  });

  it('Step 6: Should verify tournament persistence directly in Tournament Microservice DB', async function () {
    TestLogger.step(6, 'Direct API -> Tournament Service (:8082)', 'Verifying entity existence via REST endpoint');
    const allTournaments = await ApiClient.getTournaments();
    const match = allTournaments.find((t: any) => t.name === tournamentName);
    expect(match).to.not.be.undefined;
    expect(match.season_identifier).to.equal(seasonId);
    TestLogger.success(`Cross-service state confirmed in Tournament Service database (ID: ${match?.id}).`);
  });
});
