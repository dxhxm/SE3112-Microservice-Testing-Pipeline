import { WebDriver, By } from 'selenium-webdriver';
import { BasePage } from './BasePage';

export class ProfileSettingsPage extends BasePage {
  private roleToggleButton = By.xpath("//button[contains(text(), 'Upgrade to Organizer') or contains(text(), 'Revert to Viewer')]");

  constructor(driver: WebDriver) {
    super(driver);
  }

  public async open(): Promise<void> {
    await this.navigateTo('/settings/profile');
    await this.waitForVisible(this.roleToggleButton);
  }

  public async getRoleButtonText(): Promise<string> {
    const btn = await this.waitForVisible(this.roleToggleButton);
    return await btn.getText();
  }

  public async toggleRole(): Promise<void> {
    await this.click(this.roleToggleButton);
  }
}
