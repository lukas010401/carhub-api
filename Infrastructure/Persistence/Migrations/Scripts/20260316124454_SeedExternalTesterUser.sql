-- Seed external tester user
INSERT INTO users ("Id", "Email", "PasswordHash", "FullName", "PhoneNumber", "WhatsAppNumber", "Role", "IsActive", "CreatedAt", "UpdatedAt")
VALUES (
  '90000000-0000-0000-0000-000000000001',
  'external.tester@carhub.local',
  '100000.dUVaiXVNL+GwuOLJ4aQlXg==.SHBb+uT3UKCP4r5yAVtO2rRSCq9IqxZhXrbAGvChfEI=',
  'External Tester',
  '+261340000999',
  '+261340000999',
  'Seller',
  true,
  '2026-03-16T12:45:00Z',
  NULL
)
ON CONFLICT ("Email") DO NOTHING;
