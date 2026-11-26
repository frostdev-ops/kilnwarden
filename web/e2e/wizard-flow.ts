// The owner's first steps through the setup wizard (0.1.2), for the specs that only need an owner: the welcome, the
// account, then "Skip setup for now" and into the panel.
import { expect, type Page } from '@playwright/test';

/** On the wizard's welcome page (after /setup#token=... or the launcher's login): creates the owner and skips the rest. */
export async function ownerThroughWizard(page: Page, name: string, password: string) {
  await expect(page.getByRole('heading', { name: 'Set up your panel' })).toBeVisible();
  await page.getByRole('button', { name: 'Start' }).click();
  await page.getByLabel('Your name', { exact: true }).fill(name);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Password again', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByRole('heading', { name: 'How will you open your panel?' })).toBeVisible();
  await page.getByRole('button', { name: 'Skip setup for now' }).click();
  await expect(page.getByRole('heading', { name: 'Setup skipped' })).toBeVisible();
  await page.getByRole('button', { name: 'Go to the panel' }).click();
}
