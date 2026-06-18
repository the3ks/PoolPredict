import { SmtpSettingsSection, SystemSettingsShell } from "../_components/system-settings-sections";

export default function AdminSystemPage() {
  return (
    <SystemSettingsShell>
      <SmtpSettingsSection />
    </SystemSettingsShell>
  );
}
