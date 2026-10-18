import { Builder, WebDriver } from 'selenium-webdriver';
import chrome from 'selenium-webdriver/chrome';
import dotenv from 'dotenv';

dotenv.config();

export interface EnvironmentConfig {
  baseUrl: string;
  userServiceUrl: string;
  tournamentServiceUrl: string;
  streamServiceUrl: string;
  battleServiceUrl: string;
  defaultTimeoutMs: number;
  isHeadless: boolean;
}

export const Config: EnvironmentConfig = {
  baseUrl: process.env.BASE_URL || 'http://localhost:5173',
  userServiceUrl: process.env.USER_SERVICE_URL || 'http://localhost:5168',
  tournamentServiceUrl: process.env.TOURNAMENT_SERVICE_URL || 'http://localhost:8082',
  streamServiceUrl: process.env.STREAM_SERVICE_URL || 'http://localhost:5167',
  battleServiceUrl: process.env.BATTLE_SERVICE_URL || 'http://localhost:8083',
  defaultTimeoutMs: parseInt(process.env.TIMEOUT_MS || '12000', 10),
  isHeadless: process.env.HEADLESS !== 'false',
};

export async function createWebDriver(): Promise<WebDriver> {
  const options = new chrome.Options();

  if (Config.isHeadless) {
    options.addArguments('--headless=new');
  }

  options.addArguments(
    '--no-sandbox',
    '--disable-dev-shm-usage',
    '--disable-gpu',
    '--window-size=1920,1080',
    '--ignore-certificate-errors',
    '--disable-extensions'
  );

  const driver = await new Builder()
    .forBrowser('chrome')
    .setChromeOptions(options)
    .build();

  await driver.manage().setTimeouts({
    implicit: 0, // Using explicit waits exclusively to eliminate race conditions
    pageLoad: 30000,
    script: 30000,
  });

  return driver;
}
