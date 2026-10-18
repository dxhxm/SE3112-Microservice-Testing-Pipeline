import { WebDriver, By } from 'selenium-webdriver';
import { BasePage } from './BasePage';

export class LoginPage extends BasePage {
  private identifierInput = By.id('identifier');
  private passwordInput = By.id('password');
  private submitButton = By.css('button[type="submit"]');
  private errorMessage = By.css('.border-arena-crimson');
  private forgotPasswordLink = By.css('a[href="/forgot-password"]');

  constructor(driver: WebDriver) {
    super(driver);
  }

  public async open(): Promise<void> {
    await this.navigateTo('/login');
    await this.waitForVisible(this.identifierInput);
  }

  public async login(identifier: string, pass: string): Promise<void> {
    await this.sendKeys(this.identifierInput, identifier);
    await this.sendKeys(this.passwordInput, pass);
    await this.click(this.submitButton);
  }

  public async getErrorMessage(): Promise<string> {
    const el = await this.waitForVisible(this.errorMessage);
    return await el.getText();
  }

  public async hasErrorMessage(): Promise<boolean> {
    try {
      await this.waitForVisible(this.errorMessage, 4000);
      return true;
    } catch {
      return false;
    }
  }
}
