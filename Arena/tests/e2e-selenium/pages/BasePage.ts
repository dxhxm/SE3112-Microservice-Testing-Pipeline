import { WebDriver, By, until, WebElement } from 'selenium-webdriver';
import fs from 'fs';
import path from 'path';
import { Config } from '../config/selenium.config';

export abstract class BasePage {
  protected driver: WebDriver;
  protected timeout: number;

  constructor(driver: WebDriver, timeout: number = Config.defaultTimeoutMs) {
    this.driver = driver;
    this.timeout = timeout;
  }

  public async navigateTo(pathUrl: string): Promise<void> {
    const fullUrl = pathUrl.startsWith('http') ? pathUrl : `${Config.baseUrl}${pathUrl}`;
    await this.driver.get(fullUrl);
  }

  public async getCurrentUrl(): Promise<string> {
    return await this.driver.getCurrentUrl();
  }

  public async waitForElement(locator: By, timeoutMs: number = this.timeout): Promise<WebElement> {
    return await this.driver.wait(until.elementLocated(locator), timeoutMs, `Timed out waiting for element: ${locator.toString()}`);
  }

  public async waitForVisible(locator: By, timeoutMs: number = this.timeout): Promise<WebElement> {
    const el = await this.waitForElement(locator, timeoutMs);
    await this.driver.wait(until.elementIsVisible(el), timeoutMs, `Element not visible: ${locator.toString()}`);
    return el;
  }

  public async waitForClickable(locator: By, timeoutMs: number = this.timeout): Promise<WebElement> {
    const el = await this.waitForVisible(locator, timeoutMs);
    await this.driver.wait(until.elementIsEnabled(el), timeoutMs, `Element not clickable: ${locator.toString()}`);
    return el;
  }

  public async click(locator: By, timeoutMs: number = this.timeout): Promise<void> {
    const el = await this.waitForClickable(locator, timeoutMs);
    await el.click();
  }

  public async sendKeys(locator: By, text: string, clearFirst: boolean = true): Promise<void> {
    const el = await this.waitForVisible(locator);
    if (clearFirst) {
      await el.clear();
    }
    await el.sendKeys(text);
  }

  public async getText(locator: By): Promise<string> {
    const el = await this.waitForVisible(locator);
    return await el.getText();
  }

  public async waitForUrlContains(fragment: string, timeoutMs: number = this.timeout): Promise<boolean> {
    return await this.driver.wait(
      until.urlContains(fragment),
      timeoutMs,
      `URL did not contain "${fragment}" within ${timeoutMs}ms. Current: ${await this.driver.getCurrentUrl()}`
    );
  }

  public async takeScreenshot(name: string): Promise<string> {
    const reportsDir = path.resolve(process.cwd(), 'mochawesome-report', 'screenshots');
    if (!fs.existsSync(reportsDir)) {
      fs.mkdirSync(reportsDir, { recursive: true });
    }
    const filename = `${name.replace(/[^a-z0-9]/gi, '_').toLowerCase()}_${Date.now()}.png`;
    const filepath = path.join(reportsDir, filename);
    const screenshotData = await this.driver.takeScreenshot();
    fs.writeFileSync(filepath, screenshotData, 'base64');
    return filepath;
  }
}
