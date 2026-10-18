import { WebDriver, By } from 'selenium-webdriver';
import { BasePage } from './BasePage';

export class SignupPage extends BasePage {
  private usernameInput = By.id('username');
  private emailInput = By.id('email');
  private passwordInput = By.id('password');
  private confirmPasswordInput = By.id('confirmPassword');
  private submitButton = By.css('button[type="submit"]');
  private errorMessage = By.css('.border-arena-crimson');

  constructor(driver: WebDriver) {
    super(driver);
  }

  public async open(): Promise<void> {
    await this.navigateTo('/signup');
    await this.waitForVisible(this.usernameInput);
  }

  public async signup(user: string, email: string, pass: string, confirmPass?: string): Promise<void> {
    await this.sendKeys(this.usernameInput, user);
    await this.sendKeys(this.emailInput, email);
    await this.sendKeys(this.passwordInput, pass);
    await this.sendKeys(this.confirmPasswordInput, confirmPass || pass);
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
