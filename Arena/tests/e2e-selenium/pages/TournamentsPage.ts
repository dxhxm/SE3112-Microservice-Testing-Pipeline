import { WebDriver, By } from 'selenium-webdriver';
import { BasePage } from './BasePage';

export class TournamentsPage extends BasePage {
  private pageHeading = By.xpath("//h1[contains(text(), 'Tournaments')]");
  private createTournamentBtn = By.xpath("//button[contains(text(), 'Create Tournament')]");
  private modalHeader = By.xpath("//h2[contains(text(), 'Create Tournament')]");
  private tournamentNameInput = By.xpath("//label[contains(text(), 'Name')]/following-sibling::input");
  private seasonIdentifierInput = By.xpath("//label[contains(text(), 'Season')]/following-sibling::input");
  private modalSubmitBtn = By.xpath("//div[contains(@class, 'bg-arena-surface')]//button[@type='submit' or contains(text(), 'Create Tournament')]");

  constructor(driver: WebDriver) {
    super(driver);
  }

  public async open(): Promise<void> {
    await this.navigateTo('/tournaments');
    await this.waitForVisible(this.pageHeading);
  }

  public async isCreateButtonVisible(): Promise<boolean> {
    try {
      await this.waitForVisible(this.createTournamentBtn, 4000);
      return true;
    } catch {
      return false;
    }
  }

  public async openCreateModal(): Promise<void> {
    await this.click(this.createTournamentBtn);
    await this.waitForVisible(this.modalHeader);
  }

  public async createTournament(name: string, season: string): Promise<void> {
    await this.sendKeys(this.tournamentNameInput, name);
    await this.sendKeys(this.seasonIdentifierInput, season);
    await this.click(this.modalSubmitBtn);
  }

  public async isTournamentPresent(name: string): Promise<boolean> {
    try {
      const card = By.xpath(`//h2[contains(text(), '${name}')]`);
      await this.waitForVisible(card, 8000);
      return true;
    } catch {
      return false;
    }
  }
}
