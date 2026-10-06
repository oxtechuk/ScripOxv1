// ============================================================
// ScripOx — Auto-Scroll & Deep Crawl Engine
// Automatically scrolls Google Maps feeds & web directories
// to progressively load all pages and continuous records.
// ============================================================

import type { ExtractedRecord } from '../../types';
import { GoogleMapsDetector } from '../detectors/GoogleMapsDetector';

export class AutoCrawler {
  private static activeInterval: number | null = null;
  private static isCrawling: boolean = false;
  private static scrollCount: number = 0;
  private static previousCount: number = 0;
  private static stalledAttempts: number = 0;

  /**
   * Check if crawling is currently in progress
   */
  public static isRunning(): boolean {
    return this.isCrawling;
  }

  /**
   * Start auto-scrolling & lead extraction loop
   */
  public static start(
    onProgress: (leads: ExtractedRecord[], scrollCount: number) => void,
    onComplete: (totalLeads: number) => void,
    maxScrolls: number = 25
  ): void {
    if (this.isCrawling) return;

    this.isCrawling = true;
    this.scrollCount = 0;
    this.previousCount = 0;
    this.stalledAttempts = 0;

    const isMaps = GoogleMapsDetector.isGoogleMapsPage();

    // Run first extraction immediately
    let initialLeads: ExtractedRecord[] = [];
    if (isMaps) {
      initialLeads = GoogleMapsDetector.extractGoogleMapsLeads();
      if (initialLeads.length > 0) {
        onProgress(initialLeads, 0);
        this.previousCount = initialLeads.length;
      }
    }

    this.activeInterval = window.setInterval(() => {
      if (!this.isCrawling) {
        this.stop();
        return;
      }

      this.scrollCount++;

      // ── 1. Scroll Google Maps feed or regular page ──────────
      if (isMaps) {
        // Find Google Maps scrollable results pane
        const feed =
          (document.querySelector('div[role="feed"]') as HTMLElement | null) ||
          (document.querySelector('.m6QErb[aria-label]') as HTMLElement | null) ||
          (document.querySelector('div.m6QErb.DJAJbe') as HTMLElement | null) ||
          (document.querySelector('div.m6QErb') as HTMLElement | null);

        if (feed) {
          feed.scrollBy({ top: 1500, behavior: 'smooth' });
        } else {
          window.scrollBy({ top: 1000, behavior: 'smooth' });
        }

        // Check for end-of-list indicator in Google Maps
        const endBanner = document.querySelector('.HlvSq, [class*="endOfResults"]');
        if (endBanner && endBanner.textContent?.includes('وصلت إلى نهاية')) {
          this.stop();
          const finalLeads = GoogleMapsDetector.extractGoogleMapsLeads();
          onProgress(finalLeads, this.scrollCount);
          onComplete(finalLeads.length);
          return;
        }

        // Extract newly rendered places
        const currentLeads = GoogleMapsDetector.extractGoogleMapsLeads();
        if (currentLeads.length > this.previousCount) {
          this.previousCount = currentLeads.length;
          this.stalledAttempts = 0;
          onProgress(currentLeads, this.scrollCount);
        } else {
          this.stalledAttempts++;
          // Still report existing leads
          if (currentLeads.length > 0) {
            onProgress(currentLeads, this.scrollCount);
          }
        }
      } else {
        // Regular Web Page (infinite scroll or next page)
        window.scrollBy({ top: 1200, behavior: 'smooth' });

        // Check if next page button exists
        const nextBtn = document.querySelector(
          'a[rel="next"], a.next, a.next-page, button.next, .pagination-next, [aria-label*="Next"]'
        ) as HTMLElement | null;

        if (nextBtn && this.scrollCount % 3 === 0) {
          try {
            nextBtn.click();
          } catch { /* ignore */ }
        }
      }

      // Stop if stalled 5 times or reached maximum scroll limit
      if (this.stalledAttempts >= 6 || this.scrollCount >= maxScrolls) {
        this.stop();
        const total = isMaps ? GoogleMapsDetector.extractGoogleMapsLeads().length : 0;
        onComplete(total);
      }
    }, 1800);
  }

  /**
   * Stop crawling loop
   */
  public static stop(): void {
    if (this.activeInterval !== null) {
      clearInterval(this.activeInterval);
      this.activeInterval = null;
    }
    this.isCrawling = false;
  }
}
