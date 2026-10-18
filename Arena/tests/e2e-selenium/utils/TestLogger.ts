export class TestLogger {
  private static colors = {
    reset: '\x1b[0m',
    bright: '\x1b[1m',
    dim: '\x1b[2m',
    red: '\x1b[31m',
    green: '\x1b[32m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    cyan: '\x1b[36m',
    magenta: '\x1b[35m',
  };

  private static timestamp(): string {
    const now = new Date();
    return now.toTimeString().split(' ')[0] + '.' + String(now.getMilliseconds()).padStart(3, '0');
  }

  public static info(message: string): void {
    console.log(
      `${this.colors.dim}[${this.timestamp()}]${this.colors.reset} ` +
      `${this.colors.blue}[INFO]${this.colors.reset} ${message}`
    );
  }

  public static step(stepNumber: number, boundary: string, action: string): void {
    console.log(
      `${this.colors.dim}[${this.timestamp()}]${this.colors.reset} ` +
      `${this.colors.cyan}[STEP ${stepNumber}]${this.colors.reset} ` +
      `${this.colors.magenta}[${boundary}]${this.colors.reset} ${action}`
    );
  }

  public static success(message: string): void {
    console.log(
      `${this.colors.dim}[${this.timestamp()}]${this.colors.reset} ` +
      `${this.colors.green}[SUCCESS] \u2714 ${message}${this.colors.reset}`
    );
  }

  public static warn(message: string): void {
    console.log(
      `${this.colors.dim}[${this.timestamp()}]${this.colors.reset} ` +
      `${this.colors.yellow}[WARN] \u26A0 ${message}${this.colors.reset}`
    );
  }

  public static error(message: string): void {
    console.log(
      `${this.colors.dim}[${this.timestamp()}]${this.colors.reset} ` +
      `${this.colors.red}[ERROR] \u2716 ${message}${this.colors.reset}`
    );
  }

  public static teardown(message: string): void {
    console.log(
      `${this.colors.dim}[${this.timestamp()}]${this.colors.reset} ` +
      `${this.colors.yellow}[TEARDOWN] \u267B ${message}${this.colors.reset}`
    );
  }
}
