import { expect } from 'chai';
import { WebDriver } from 'selenium-webdriver';
import { createWebDriver } from '../config/selenium.config';
import { SignupPage } from '../pages/SignupPage';
import { LoginPage } from '../pages/LoginPage';
import { TestLogger } from '../utils/TestLogger';
import { ApiClient } from '../utils/ApiClient';

describe('TC-E2E-03: Business Rule Rejections & Duplicate Conflict Edge Cases', function () {
  this.timeout(60000);

  let driver: WebDriver;
  let signupPage: SignupPage;
  let loginPage: LoginPage;

  const timestamp = Date.now();
  const existingUser = `e2e_dup_${timestamp}`;
  const existingEmail = `dup_${timestamp}@arena.test`;
  const password = 'Password123!';

  let authToken: string = '';

  before(async function () {
    TestLogger.info('Initializing Selenium Chrome WebDriver for Conflict test...');
    driver = await createWebDriver();
    signupPage = new SignupPage(driver);
    loginPage = new LoginPage(driver);
  });

  after(async function () {
    if (authToken) {
      TestLogger.teardown(`Cleaning up account [${existingUser}]...`);
      try {
        await ApiClient.deleteAccount(authToken);
        TestLogger.teardown('Teardown finished.');
      } catch (err: any) {
        TestLogger.warn(`Teardown warning: ${err.message}`);
      }
    }

    if (driver) {
      await driver.quit();
    }
  });

  it('Step 1: Should seed an initial user in User Service', async function () {
    TestLogger.step(1, 'User Service (:5168)', `Creating base user "${existingUser}"`);
    await signupPage.open();
    await signupPage.signup(existingUser, existingEmail, password);

    await loginPage.waitForUrlContains('/login', 15000);
    const loginRes = await ApiClient.login(existingUser, password);
    authToken = loginRes.token;
    TestLogger.success('Base user seeded.');
  });

  it('Step 2: Should reject duplicate registration with 409 conflict & display UI banner', async function () {
    TestLogger.step(2, 'UI -> User Service (:5168)', `Attempting duplicate registration for "${existingUser}"`);
    await signupPage.open();
    // Try to register with same username but different email
    await signupPage.signup(existingUser, `other_${timestamp}@arena.test`, password);

    const hasError = await signupPage.hasErrorMessage();
    expect(hasError).to.be.true;

    const errorText = await signupPage.getErrorMessage();
    expect(errorText.toLowerCase()).to.satisfy(
      (msg: string) => msg.includes('taken') || msg.includes('exists') || msg.includes('failed') || msg.includes('conflict')
    );
    TestLogger.success(`Duplicate conflict rejected properly: "${errorText}".`);
  });
});
