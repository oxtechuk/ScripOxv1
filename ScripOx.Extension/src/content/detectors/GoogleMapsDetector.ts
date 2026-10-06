// ============================================================
// ScripOx — Specialized Google Maps & Directory Detector
// High-precision automatic extraction of businesses from Google Maps
// ============================================================

import type { ExtractedRecord } from '../../types';
import { DataCleaner } from '../engines/DataCleaner';

export class GoogleMapsDetector {
  /**
   * Checks if current page is Google Maps
   */
  public static isGoogleMapsPage(): boolean {
    return (
      window.location.hostname.includes('google.') &&
      (window.location.pathname.includes('/maps') || window.location.search.includes('maps'))
    );
  }

  /**
   * Extracts all business leads currently visible on the Google Maps page
   */
  public static extractGoogleMapsLeads(): ExtractedRecord[] {
    const records: ExtractedRecord[] = [];

    // Find all place cards in the sidebar/feed
    // Standard Google Maps classes: div.Nv2PK, div[role="article"], div[jsaction*="mouseover:pane"]
    const cardNodes = document.querySelectorAll(
      'div.Nv2PK, div[role="article"], div.m6QErb div[jsaction*="pane"], div.bfTFDd'
    );

    const items = Array.from(cardNodes).filter((el) => {
      // Must contain a headline/title or link with aria-label
      return (
        el.querySelector('.qBF1Pd, .fontHeadlineSmall, a.hfpxzc') !== null ||
        el.getAttribute('aria-label') !== null
      );
    });

    for (const card of items) {
      const rec: ExtractedRecord = {};

      // 1. Business Name
      const nameEl = card.querySelector('.qBF1Pd, .fontHeadlineSmall, [class*="title"]');
      const linkEl = card.querySelector('a.hfpxzc') as HTMLAnchorElement | null;
      const rawName = nameEl?.textContent || linkEl?.getAttribute('aria-label') || '';
      rec['name'] = DataCleaner.cleanText(rawName);

      if (!rec['name'] || rec['name'].length < 2) continue;

      // 2. Rating & Reviews
      const ratingEl = card.querySelector('span.MW4etd, [aria-label*="stars"], [aria-label*="نجوم"]');
      const reviewsEl = card.querySelector('span.UY7F9, [aria-label*="reviews"], [aria-label*="تقييم"]');
      if (ratingEl) rec['rating'] = ratingEl.textContent?.trim() || '';
      if (reviewsEl) rec['reviews'] = reviewsEl.textContent?.replace(/[()]/g, '').trim() || '';

      // 3. Category & Address details from secondary containers (.W4Efsd)
      const detailContainers = Array.from(card.querySelectorAll('.W4Efsd'));
      const textLines: string[] = [];
      for (const d of detailContainers) {
        const txt = DataCleaner.cleanText(d.textContent || '');
        if (txt && !textLines.includes(txt)) textLines.push(txt);
      }

      // First line typically contains Category / Type (e.g. "Real Estate Agency · Dubai")
      if (textLines.length > 0) {
        const parts = textLines[0].split('·').map((s) => s.trim());
        if (parts.length > 0) {
          rec['category'] = parts[0];
        }
        if (parts.length > 1) {
          rec['address'] = parts.slice(1).join(', ');
        }
      }

      if (textLines.length > 1 && !rec['address']) {
        rec['address'] = textLines[1];
      }

      // 4. Phone Number — Search inside all text elements in the card
      const fullCardText = card.textContent || '';
      const phoneMatch = fullCardText.match(/(?:(?:\+|00)\d{1,3}[\s.-]?)?\(?\d{2,5}\)?[\s.-]?\d{3,4}[\s.-]?\d{3,4}/);
      if (phoneMatch) {
        rec['phone'] = DataCleaner.cleanPhone(phoneMatch[0]);
      } else {
        rec['phone'] = '';
      }

      // 5. Website Link
      const websiteLink = card.querySelector(
        'a[data-value="Website"], a[aria-label*="website"], a[href^="http"]:not([href*="google.com/maps"])'
      ) as HTMLAnchorElement | null;
      if (websiteLink && websiteLink.href) {
        rec['website'] = DataCleaner.cleanWebsite(websiteLink.href);
      } else {
        rec['website'] = '';
      }

      // 6. Coordinates (Latitude / Longitude) and Google Maps URL
      if (linkEl && linkEl.href) {
        rec['maps_url'] = linkEl.href;
        // Parse coordinates from URL e.g. !3d25.2048!4d55.2708 or @25.2048,55.2708
        const coordMatch =
          linkEl.href.match(/!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)/) ||
          linkEl.href.match(/@(-?\d+\.\d+),(-?\d+\.\d+)/);
        if (coordMatch) {
          rec['latitude'] = coordMatch[1];
          rec['longitude'] = coordMatch[2];
        }
      }

      // Clean record with DataCleaner
      const cleanedRecord = DataCleaner.cleanRecord(rec);
      records.push(cleanedRecord);
    }

    return DataCleaner.cleanAndDeduplicate(records);
  }
}
