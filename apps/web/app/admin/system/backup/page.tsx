import { DatabaseBackupSection, SystemSettingsShell } from "../../_components/system-settings-sections";

export default function AdminSystemBackupPage() {
  return (
    <SystemSettingsShell>
      <DatabaseBackupSection />
    </SystemSettingsShell>
  );
}
