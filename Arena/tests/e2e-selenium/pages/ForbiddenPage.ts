import { WebDriver, By } from 'selenium-webdriver';
import { BasePage } from './BasePage';

export class ForbiddenPage extends BasePage {
  private heading = By.xpath("//h2[contains(text(), 'Access Denied')]");
  private message = By.xpath("//p[contains(text(), 'required permissions')]");
  private returnButton = By.xpath("//button[contains(text(), 'Return to Dashboard')]");

  constructor(driver: WebDriver) {
    super(driver);
  }

  public async isAccessDeniedDisplayed(): Promise<boolean> {
    try {
      await this.waitForVisible(this.heading, 5000);
      return true;
    } catch {
      return false;
    }
  }

  public async getMessageText(): Promise<string> {
    const el = await this.waitForVisible(this.message);
    return await el.getText();
  }

  public async clickReturnToDashboard(): Promise<void> {
    await this.click(this.returnButton);
  }
}
