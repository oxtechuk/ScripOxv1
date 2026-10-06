// ============================================================
// ScripOx — Lead Data Cleaner & Normalization Engine
// Sanitizes raw scraped records: phone, email, website, address
// Removes duplicate entities and Google tracking redirects
// ============================================================

import type { ExtractedRecord } from '../../types';

export class DataCleaner {
  /**
   * Sanitizes a single raw record
   */
  public static cleanRecord(raw: ExtractedRecord): ExtractedRecord {
    const clean: ExtractedRecord = {};

    for (const [key, val] of Object.entries(raw)) {
      if (typeof val !== 'string') {
        clean[key] = val;
        continue;
      }

      const lowerKey = key.toLowerCase();

      if (lowerKey.includes('phone') || lowerKey.includes('tel') || lowerKey.includes('mobile')) {
        clean[key] = this.cleanPhone(val);
      } else if (lowerKey.includes('email') || lowerKey.includes('mail')) {
        clean[key] = this.cleanEmail(val);
      } else if (lowerKey.includes('website') || lowerKey.includes('url') || lowerKey.includes('link') || lowerKey.includes('web')) {
        clean[key] = this.cleanWebsite(val);
      } else {
        clean[key] = this.cleanText(val);
      }
    }

    // Auto-detect phone / email inside other text fields if not present
    if (!clean['phone'] || clean['phone'].length < 5) {
      const detectedPhone = this.findPhoneInRecord(clean);
      if (detectedPhone) clean['phone'] = detectedPhone;
    }

    if (!clean['email'] || !clean['email'].includes('@')) {
      const detectedEmail = this.findEmailInRecord(clean);
      if (detectedEmail) clean['email'] = detectedEmail;
    }

    return clean;
  }

  /**
   * Cleans an array of records and removes duplicates
   */
  public static cleanAndDeduplicate(records: ExtractedRecord[]): ExtractedRecord[] {
    const seen = new Set<string>();
    const cleanedList: ExtractedRecord[] = [];

    for (const record of records) {
      const cleaned = this.cleanRecord(record);

      // Generate a fingerprint for deduplication
      const phoneKey = (cleaned['phone'] || '').replace(/\D/g, '');
      const emailKey = (cleaned['email'] || '').toLowerCase().trim();
      const nameKey = (cleaned['name'] || cleaned['title'] || cleaned['business name'] || '')
        .toLowerCase()
        .replace(/[^a-z0-9]/g, '');

      let key = '';
      if (phoneKey.length >= 7) {
        key = `p:${phoneKey}`;
      } else if (emailKey.length > 5) {
        key = `e:${emailKey}`;
      } else if (nameKey.length > 2) {
        key = `n:${nameKey}`;
      } else {
        key = JSON.stringify(cleaned);
      }

      if (!seen.has(key)) {
        seen.add(key);
        cleanedList.push(cleaned);
      }
    }

    return cleanedList;
  }

  /**
   * Normalizes phone numbers (UAE, German, UK, and international)
   */
  public static cleanPhone(val: string): string {
    if (!val) return '';
    // Strip prefixes like "Phone:", "Tel:", "Call:", "هاتف:"
    let cleaned = val.replace(/(?:phone|tel|telephone|call|mobile|fax|هاتف|اتصال)\s*[:.-]?\s*/gi, '');
    
    // Find phone pattern: optional +, followed by digits, spaces, dashes
    const match = cleaned.match(/(\+?\d[\d\s\-().]{6,}\d)/);
    if (match) {
      cleaned = match[1].trim();
    } else {
      cleaned = cleaned.trim();
    }

    // Collapse multiple internal spaces
    cleaned = cleaned.replace(/\s+/g, ' ');

    // If string has letters after stripping, remove letters
    cleaned = cleaned.replace(/[^\d+\s\-()]/g, '').trim();

    return cleaned;
  }

  /**
   * Extracts clean valid email
   */
  public static cleanEmail(val: string): string {
    if (!val) return '';
    const match = val.match(/[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}/);
    return match ? match[0].toLowerCase().trim() : '';
  }

  /**
   * Unwraps Google redirect URLs and cleans tracking parameters
   */
  public static cleanWebsite(url: string): string {
    if (!url) return '';
    let target = url.trim();

    // Check for Google redirect wrapper: /url?q=https://example.com
    try {
      if (target.includes('google.com/url?')) {
        const parsed = new URL(target, window.location.origin);
        const qParam = parsed.searchParams.get('q') || parsed.searchParams.get('url');
        if (qParam) target = qParam;
      }
    } catch {
      // Fallback regex
      const match = target.match(/[?&](?:q|url)=(https?[^&]+)/);
      if (match) target = decodeURIComponent(match[1]);
    }

    // Remove tracking queries
    try {
      const u = new URL(target);
      const paramsToRemove = ['utm_source', 'utm_medium', 'utm_campaign', 'utm_term', 'utm_content', 'fbclid', 'gclid'];
      for (const p of paramsToRemove) {
        u.searchParams.delete(p);
      }
      return u.toString().replace(/\/$/, ''); // strip trailing slash
    } catch {
      return target;
    }
  }

  /**
   * General string cleanup: removes excess whitespace and non-breaking spaces
   */
  public static cleanText(val: string): string {
    if (!val) return '';
    return val
      .replace(/&nbsp;/gi, ' ')
      .replace(/[\r\n\t]+/g, ' ')
      .replace(/\s{2,}/g, ' ')
      .trim();
  }

  private static findPhoneInRecord(rec: ExtractedRecord): string | null {
    for (const [k, v] of Object.entries(rec)) {
      if (k === 'phone' || typeof v !== 'string') continue;
      const match = v.match(/(?:(?:\+|00)\d{1,3}[\s.-]?)?\(?\d{2,5}\)?[\s.-]?\d{3,4}[\s.-]?\d{3,4}/);
      if (match && match[0].replace(/\D/g, '').length >= 7) {
        return match[0].trim();
      }
    }
    return null;
  }

  private static findEmailInRecord(rec: ExtractedRecord): string | null {
    for (const [k, v] of Object.entries(rec)) {
      if (k === 'email' || typeof v !== 'string') continue;
      const match = v.match(/[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}/);
      if (match) return match[0].toLowerCase().trim();
    }
    return null;
  }
}
