"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { Database, Mail, Save, Send, Settings } from "lucide-react";
import { IconLabel, Panel } from "../../components/ui";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";
import { formatDisplayDateTime } from "../../lib/datetime";
import { SystemSettingsSection as SmtpSettingsSection } from "./admin-sections";

type DatabaseBackupSettings = {
  recipientEmail: string;
  updatedAt: string | null;
  lastSentAt: string | null;
};

export { SmtpSettingsSection };

const systemNavItems = [
  { href: "/admin/system", label: "SMTP settings", icon: Mail },
  { href: "/admin/system/backup", label: "Database backup", icon: Database },
];

export function SystemSettingsShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  return (
    <div className="pageStack">
      <Panel title="System settings">
        <div className="pageHeader systemSectionHeader">
          <div className="pageTitleGroup">
            <span className="pageIcon">
              <Settings aria-hidden="true" size={18} />
            </span>
            <div>
              <h1>System settings</h1>
              <p className="mutedText">Manage delivery settings and database backup operations.</p>
            </div>
          </div>
        </div>
        <nav className="sectionSubnav" aria-label="System settings sections">
          {systemNavItems.map((item) => (
            <Link className={pathname === item.href ? "active" : ""} href={item.href} key={item.href}>
              <IconLabel icon={item.icon}>{item.label}</IconLabel>
            </Link>
          ))}
        </nav>
      </Panel>
      {children}
    </div>
  );
}

export function DatabaseBackupSection() {
  const [recipientEmail, setRecipientEmail] = useState("");
  const [message, setMessage] = useState("Loading backup settings...");
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const [lastSentAt, setLastSentAt] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isSending, setIsSending] = useState(false);

  useEffect(() => {
    void loadSettings();
  }, []);

  async function loadSettings() {
    const token = getStoredToken();
    if (!token) {
      setMessage("Session is missing.");
      return;
    }

    try {
      const response = await fetch(apiUrl("/api/admin/database-backup/settings"), {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setMessage(await readApiError(response, "Could not load backup settings."));
        return;
      }

      const result = (await response.json()) as DatabaseBackupSettings;
      setRecipientEmail(result.recipientEmail);
      setUpdatedAt(result.updatedAt);
      setLastSentAt(result.lastSentAt);
      setMessage(result.recipientEmail ? "Backup settings loaded." : "Enter a recipient email to enable emailed backups.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not load backup settings.");
    }
  }

  async function saveSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token) {
      setMessage("Session is missing.");
      return;
    }

    setIsSaving(true);
    setMessage("Saving backup recipient...");
    try {
      const response = await fetch(apiUrl("/api/admin/database-backup/settings"), {
        method: "PUT",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ recipientEmail }),
      });

      if (!response.ok) {
        setMessage(await readApiError(response, "Could not save backup recipient."));
        return;
      }

      const result = (await response.json()) as DatabaseBackupSettings;
      setRecipientEmail(result.recipientEmail);
      setUpdatedAt(result.updatedAt);
      setLastSentAt(result.lastSentAt);
      setMessage("Backup recipient saved.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not save backup recipient.");
    } finally {
      setIsSaving(false);
    }
  }

  async function sendBackup() {
    const token = getStoredToken();
    if (!token) {
      setMessage("Session is missing.");
      return;
    }

    setIsSending(true);
    setMessage("Generating database backup...");
    try {
      const response = await fetch(apiUrl("/api/admin/database-backup/send"), {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ recipientEmail }),
      });

      if (!response.ok) {
        setMessage(await readApiError(response, "Could not generate database backup."));
        return;
      }

      const result = (await response.json()) as { message: string; recipientEmail: string };
      await loadSettings();
      setMessage(result.message);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not generate database backup.");
    } finally {
      setIsSending(false);
    }
  }

  return (
    <Panel title="Database backup">
      <p className="statusText">{message}</p>
      <form className="form" onSubmit={saveSettings}>
        <label>
          Backup email
          <input
            placeholder="admin@example.com"
            required
            type="email"
            value={recipientEmail}
            onChange={(event) => setRecipientEmail(event.target.value)}
          />
        </label>
        <dl className="detailList">
          <div>
            <dt>Backup file</dt>
            <dd><code>poolpredict_db_YYYYMMdd-HHmm.zip</code></dd>
          </div>
          <div>
            <dt>Contents</dt>
            <dd>Full MariaDB structure and data SQL dump, zipped before sending.</dd>
          </div>
          <div>
            <dt>Recipient saved</dt>
            <dd>{updatedAt ? formatDisplayDateTime(updatedAt) : "Not saved yet"}</dd>
          </div>
          <div>
            <dt>Last emailed</dt>
            <dd>{lastSentAt ? formatDisplayDateTime(lastSentAt) : "Not sent yet"}</dd>
          </div>
        </dl>
        <div className="buttonRow">
          <button className="button" disabled={isSaving} type="submit">
            <IconLabel icon={Save}>Save recipient</IconLabel>
          </button>
          <button className="button buttonSecondary" disabled={!recipientEmail || isSending} type="button" onClick={sendBackup}>
            <IconLabel icon={Send}>{isSending ? "Generating backup..." : "Generate and email backup"}</IconLabel>
          </button>
        </div>
        <p className="mutedText">
          The API host must have `mariadb-dump` or `mysqldump` available, unless `DatabaseBackup:DumpExecutablePath` is configured.
        </p>
      </form>
    </Panel>
  );
}
