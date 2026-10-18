import { expect } from 'chai';
import { WebDriver } from 'selenium-webdriver';
import { createWebDriver } from '../config/selenium.config';
import { SignupPage } from '../pages/SignupPage';
import { LoginPage } from '../pages/LoginPage';
import { ForbiddenPage } from '../pages/ForbiddenPage';
import { TestLogger } from '../utils/TestLogger';
import { ApiClient } from '../utils/ApiClient';

describe('TC-E2E-02: Role-Based Access Control (RBAC) & 403 Forbidden Route Guard', function () {
  this.timeout(60000);

  let driver: WebDriver;
  let signupPage: SignupPage;
  let loginPage: LoginPage;
  let forbiddenPage: ForbiddenPage;

  const timestamp = Date.now();
  const viewerUser = `e2e_viewer_${timestamp}`;
  const viewerEmail = `viewer_${timestamp}@arena.test`;
  const viewerPassword = 'Password123!';

  let authToken: string = '';

  before(async function () {
    TestLogger.info('Initializing Selenium Chrome WebDriver for RBAC test...');
    driver = await createWebDriver();
    signupPage = new SignupPage(driver);
    loginPage = new LoginPage(driver);
    forbiddenPage = new ForbiddenPage(driver);
  });

  after(async function () {
    if (authToken) {
      TestLogger.teardown(`Cleaning up test viewer account [${viewerUser}]...`);
      try {
        await ApiClient.deleteAccount(authToken);
        TestLogger.teardown('Viewer account purged successfully.');
      } catch (err: any) {
        TestLogger.warn(`Teardown warning: ${err.message}`);
      }
    }

    if (driver) {
      await driver.quit();
    }
  });

  it('Step 1: Should register a Viewer account (default role: Viewer)', async function () {
    TestLogger.step(1, 'UI -> User Service (:5168)', `Creating standard Viewer account "${viewerUser}"`);
    await signupPage.open();
    await signupPage.signup(viewerUser, viewerEmail, viewerPassword);

    await loginPage.waitForUrlContains('/login', 15000);
    TestLogger.success('Viewer account registered.');
  });

  it('Step 2: Should login as Viewer', async function () {
    TestLogger.step(2, 'UI -> User Service (:5168)', `Logging in as "${viewerUser}"`);
    await loginPage.open();
    await loginPage.login(viewerUser, viewerPassword);
    await loginPage.waitForUrlContains('/', 15000);

    const loginRes = await ApiClient.login(viewerUser, viewerPassword);
    authToken = loginRes.token;
    TestLogger.success('Viewer logged in.');
  });

  it('Step 3: Should block access to Organizer match schedule route and redirect to /403', async function () {
    TestLogger.step(3, 'Frontend Route Guard -> RBAC', 'Attempting direct navigation to /organizer/matches/new');
    await forbiddenPage.navigateTo('/organizer/matches/new');

    await forbiddenPage.waitForUrlContains('/403', 10000);
    const isDenied = await forbiddenPage.isAccessDeniedDisplayed();
    expect(isDenied).to.be.true;

    const message = await forbiddenPage.getMessageText();
    expect(message).to.include('required permissions');
    TestLogger.success('Access Denied route guard verified. Viewer correctly prevented from organizer actions.');
  });
});
