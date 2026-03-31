import { useCallback, useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { Api } from '../api/client';
import type { Damage, Rental, Vehicle } from '../api/types';
import { LoadingView } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
import { StatCard } from '../components/StatCard';
import { formatCurrency, formatShortDate } from '../utils/format';
import { DEFAULT_DAMAGE_DESCRIPTION } from '../utils/referenceData';

const PAGE_SIZE = 25;
const MAX_DAMAGE_PHOTOS = 5;
const DAMAGE_PHOTO_LIMIT_MESSAGE = `Можна додати не більше ${MAX_DAMAGE_PHOTOS} фото до одного акту.`;

export function DamagesPage() {
  const [loading, setLoading] = useState(true);
  const [rentalsLoading, setRentalsLoading] = useState(false);
  const [galleryLoading, setGalleryLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [damages, setDamages] = useState<Damage[]>([]);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [rentals, setRentals] = useState<Rental[]>([]);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [galleryDamage, setGalleryDamage] = useState<Damage | null>(null);
  const [galleryIndex, setGalleryIndex] = useState(0);
  const [galleryUrls, setGalleryUrls] = useState<string[]>([]);
  const [form, setForm] = useState({
    vehicleId: '',
    rentalId: '',
    description: DEFAULT_DAMAGE_DESCRIPTION,
    repairCost: '',
    autoChargeToRental: false,
  });
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const skipNextLoadRef = useRef(false);

  const load = useCallback(async (pageToLoad: number): Promise<boolean> => {
    try {
      setLoading(true);
      setError(null);
      const [damageData, vehicleData] = await Promise.all([
        Api.getDamagesPage({ page: pageToLoad, pageSize: PAGE_SIZE }),
        Api.getVehicles(),
      ]);
      setDamages(damageData.items);
      setPage(damageData.page);
      setTotalCount(damageData.totalCount);
      setVehicles(vehicleData);
      setSuccessMessage(null);
      return true;
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
      return false;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (skipNextLoadRef.current) {
      skipNextLoadRef.current = false;
      return;
    }

    void load(page);
  }, [load, page]);

  useEffect(() => {
    const selectedVehicleId = Number(form.vehicleId);
    if (!form.vehicleId || Number.isNaN(selectedVehicleId)) {
      setRentals([]);
      setRentalsLoading(false);
      setForm((prev) => (prev.rentalId ? { ...prev, rentalId: '' } : prev));
      return;
    }

    let isCancelled = false;

    const loadVehicleRentals = async (): Promise<void> => {
      try {
        setRentals([]);
        setRentalsLoading(true);
        setError(null);
        const rentalsForVehicle = await Api.getRentals({ vehicleId: selectedVehicleId });
        if (isCancelled) {
          return;
        }

        setRentals(rentalsForVehicle);
        setForm((prev) => {
          if (prev.vehicleId !== form.vehicleId) {
            return prev;
          }

          return rentalsForVehicle.some((rental) => String(rental.id) === prev.rentalId)
            ? prev
            : { ...prev, rentalId: '' };
        });
      } catch (requestError) {
        if (isCancelled) {
          return;
        }

        setRentals([]);
        setError(Api.errorMessage(requestError));
      } finally {
        if (!isCancelled) {
          setRentalsLoading(false);
        }
      }
    };

    void loadVehicleRentals();

    return () => {
      isCancelled = true;
    };
  }, [form.vehicleId]);

  useEffect(() => {
    if (!galleryDamage) {
      setGalleryUrls([]);
      setGalleryLoading(false);
      return;
    }

    let isCancelled = false;
    const objectUrls: string[] = [];
    setGalleryUrls([]);

    const loadGalleryPhotos = async (): Promise<void> => {
      try {
        setGalleryLoading(true);
        const urls = await Promise.all(
          galleryDamage.photos.map(async (photo) => {
            const blob = await Api.getDamagePhotoBlob(galleryDamage.id, photo.id);
            const objectUrl = URL.createObjectURL(blob);
            objectUrls.push(objectUrl);
            return objectUrl;
          }),
        );

        if (!isCancelled) {
          setGalleryUrls(urls);
        }
      } catch (requestError) {
        if (!isCancelled) {
          setGalleryDamage(null);
          setGalleryUrls([]);
          setError(Api.errorMessage(requestError));
        }
      } finally {
        if (!isCancelled) {
          setGalleryLoading(false);
        }
      }
    };

    void loadGalleryPhotos();

    return () => {
      isCancelled = true;
      for (const objectUrl of objectUrls) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [galleryDamage]);

  const clearSelectedFiles = (): void => {
    setSelectedFiles([]);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }

    setError((prev) => (prev === DAMAGE_PHOTO_LIMIT_MESSAGE ? null : prev));
  };

  const handleFileSelection = (event: ChangeEvent<HTMLInputElement>): void => {
    const incomingFiles = Array.from(event.target.files ?? []);
    if (incomingFiles.length === 0) {
      return;
    }

    const availableSlots = Math.max(0, MAX_DAMAGE_PHOTOS - selectedFiles.length);
    const acceptedFiles = incomingFiles.slice(0, availableSlots);
    if (acceptedFiles.length > 0) {
      setSelectedFiles((prev) => [...prev, ...acceptedFiles]);
    }

    if (incomingFiles.length > availableSlots) {
      setError(DAMAGE_PHOTO_LIMIT_MESSAGE);
    } else {
      setError((prev) => (prev === DAMAGE_PHOTO_LIMIT_MESSAGE ? null : prev));
    }

    event.target.value = '';
  };

  const removeSelectedFile = (indexToRemove: number): void => {
    setSelectedFiles((prev) => prev.filter((_, index) => index !== indexToRemove));
    setError((prev) => (prev === DAMAGE_PHOTO_LIMIT_MESSAGE ? null : prev));
  };

  const openGallery = (damage: Damage): void => {
    if (damage.photos.length === 0) {
      return;
    }

    setGalleryIndex(0);
    setGalleryDamage(damage);
  };

  const closeGallery = (): void => {
    setGalleryDamage(null);
    setGalleryIndex(0);
  };

  const addDamage = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    setError(null);
    setSuccessMessage(null);

    try {
      await Api.addDamage({
        vehicleId: Number(form.vehicleId),
        rentalId: form.rentalId ? Number(form.rentalId) : null,
        description: form.description,
        repairCost: Number(form.repairCost),
        autoChargeToRental: form.autoChargeToRental,
        photos: selectedFiles,
      });
      setForm((prev) => ({ ...prev, repairCost: '' }));
      clearSelectedFiles();

      if (page !== 1) {
        const refreshed = await load(1);
        skipNextLoadRef.current = true;
        setPage(1);
        if (!refreshed) {
          setSuccessMessage('Акт збережено, але журнал пошкоджень не вдалося оновити. Оновіть сторінку.');
        }
        return;
      }

      const refreshed = await load(1);
      if (!refreshed) {
        setSuccessMessage('Акт збережено, але журнал пошкоджень не вдалося оновити. Оновіть сторінку.');
      }
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  if (loading) {
    return <LoadingView text="Завантаження пошкоджень..." />;
  }

  const isRentalSelectionEnabled = Boolean(form.vehicleId) && !rentalsLoading;
  const currentGalleryPhoto = galleryDamage?.photos[galleryIndex] ?? null;
  const currentGalleryUrl = galleryUrls[galleryIndex] ?? null;

  return (
    <div className="page-grid">
      <section className="stats-grid">
        <StatCard label="Записи пошкоджень" value={totalCount} accent="blue" />
        <StatCard
          label="Відкриті"
          value={damages.filter((item) => item.status !== 'Resolved').length}
          accent="red"
        />
        <StatCard
          label="Нараховано клієнтам"
          value={formatCurrency(damages.reduce((sum, item) => sum + item.chargedAmount, 0))}
          accent="amber"
        />
      </section>

      <div className="two-col-grid">
        <Panel title="Додати пошкодження" subtitle="Фіксація акту та опціональне нарахування на оренду.">
          <form className="form-grid" onSubmit={(event) => void addDamage(event)}>
            <label>
              Авто
              <select
                required
                value={form.vehicleId}
                onChange={(event) => setForm((prev) => ({ ...prev, vehicleId: event.target.value, rentalId: '' }))}
              >
                <option value="">Оберіть авто</option>
                {vehicles.map((vehicle) => (
                  <option key={vehicle.id} value={vehicle.id}>
                    {vehicle.make} {vehicle.model} [{vehicle.licensePlate}]
                  </option>
                ))}
              </select>
            </label>

            <label>
              Оренда (опційно)
              <select
                value={form.rentalId}
                disabled={!isRentalSelectionEnabled}
                onChange={(event) => setForm((prev) => ({ ...prev, rentalId: event.target.value }))}
              >
                {!form.vehicleId ? (
                  <option value="">Спершу оберіть авто</option>
                ) : rentalsLoading ? (
                  <option value="">Завантаження оренд...</option>
                ) : (
                  <>
                    <option value="">Без прив'язки</option>
                    {rentals.map((rental) => (
                      <option key={rental.id} value={rental.id}>
                        {rental.contractNumber} ({rental.status})
                      </option>
                    ))}
                  </>
                )}
              </select>
            </label>

            <label>
              Вартість ремонту
              <input required type="number" min={0.01} step="0.01" value={form.repairCost} onChange={(event) => setForm((prev) => ({ ...prev, repairCost: event.target.value }))} />
            </label>

            <label className="full-row">
              Опис
              <input required value={form.description} onChange={(event) => setForm((prev) => ({ ...prev, description: event.target.value }))} />
            </label>

            <div className="full-row damage-photo-picker">
              <span className="damage-photo-picker-label">Фото пошкодження</span>
              <input
                ref={fileInputRef}
                hidden
                type="file"
                multiple
                accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
                onChange={handleFileSelection}
              />
              <div className="damage-photo-picker-actions">
                <button type="button" className="btn ghost" onClick={() => fileInputRef.current?.click()}>
                  Обрати фото
                </button>
                <button
                  type="button"
                  className="btn ghost"
                  disabled={selectedFiles.length === 0}
                  onClick={clearSelectedFiles}
                >
                  Очистити
                </button>
              </div>
              <p className="damage-photo-picker-hint">До 5 фото, JPG/PNG/WEBP, до 5 МБ кожне. Збереження без фото теж дозволене.</p>
              {selectedFiles.length > 0 ? (
                <ul className="damage-photo-list">
                  {selectedFiles.map((file, index) => (
                    <li key={`${file.name}-${file.size}-${file.lastModified}-${index}`} className="damage-photo-list-item">
                      <span>{file.name}</span>
                      <button type="button" className="btn ghost" onClick={() => removeSelectedFile(index)}>
                        Прибрати
                      </button>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="damage-photo-empty">Фото не вибрані.</p>
              )}
            </div>

            <label className="checkbox-row full-row">
              <input
                type="checkbox"
                checked={form.autoChargeToRental}
                onChange={(event) => setForm((prev) => ({ ...prev, autoChargeToRental: event.target.checked }))}
              />
              Автоматично нарахувати в суму оренди
            </label>

            <button type="submit" className="btn primary">Зберегти акт</button>
          </form>
        </Panel>

        <Panel title="Журнал пошкоджень" subtitle="Останні зареєстровані випадки">
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Дата</th>
                  <th>Акт</th>
                  <th>Авто</th>
                  <th>Договір</th>
                  <th>Ремонт</th>
                  <th>Нараховано</th>
                  <th>Фото</th>
                  <th>Статус</th>
                </tr>
              </thead>
              <tbody>
                {damages.map((item) => (
                  <tr key={item.id}>
                    <td>{formatShortDate(item.dateReported)}</td>
                    <td>{item.actNumber}</td>
                    <td>{item.vehicleName}</td>
                    <td>{item.contractNumber ?? '-'}</td>
                    <td>{formatCurrency(item.repairCost)}</td>
                    <td>{formatCurrency(item.chargedAmount)}</td>
                    <td>
                      {item.photos.length > 0 ? (
                        <button type="button" className="btn ghost" onClick={() => openGallery(item)}>
                          Переглянути ({item.photos.length})
                        </button>
                      ) : (
                        '-'
                      )}
                    </td>
                    <td>{item.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <PaginationControls
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            disabled={loading}
            onPageChange={setPage}
          />
        </Panel>
      </div>

      {successMessage ? <p className="success-box">{successMessage}</p> : null}
      {error ? <p className="error-box">{error}</p> : null}

      {galleryDamage ? (
        <div className="damage-gallery-backdrop" role="presentation" onClick={closeGallery}>
          <section
            className="damage-gallery"
            role="dialog"
            aria-modal="true"
            aria-labelledby="damage-gallery-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="damage-gallery-header">
              <div className="damage-gallery-copy">
                <span className="damage-gallery-kicker">Фото пошкодження</span>
                <h2 id="damage-gallery-title">{galleryDamage.actNumber}</h2>
                <p>{galleryDamage.vehicleName}</p>
              </div>
              <button type="button" className="btn ghost" onClick={closeGallery}>
                Закрити
              </button>
            </div>

            <div className="damage-gallery-stage">
              {galleryLoading || !currentGalleryUrl || !currentGalleryPhoto ? (
                <p>Завантаження фото...</p>
              ) : (
                <img
                  src={currentGalleryUrl}
                  alt={`Фото пошкодження ${galleryIndex + 1} для акту ${galleryDamage.actNumber}`}
                />
              )}
            </div>

            <div className="damage-gallery-actions">
              <button
                type="button"
                className="btn ghost"
                disabled={galleryIndex === 0}
                onClick={() => setGalleryIndex((prev) => Math.max(0, prev - 1))}
              >
                Попереднє
              </button>
              <span className="damage-gallery-counter">
                {galleryDamage.photos.length === 0 ? '0 / 0' : `${galleryIndex + 1} / ${galleryDamage.photos.length}`}
              </span>
              <button
                type="button"
                className="btn ghost"
                disabled={galleryIndex >= galleryDamage.photos.length - 1}
                onClick={() => setGalleryIndex((prev) => Math.min(galleryDamage.photos.length - 1, prev + 1))}
              >
                Наступне
              </button>
            </div>

            {galleryUrls.length > 1 ? (
              <div className="damage-gallery-strip">
                {galleryUrls.map((url, index) => (
                  <button
                    key={galleryDamage.photos[index]?.id ?? index}
                    type="button"
                    className={`damage-gallery-thumb${index === galleryIndex ? ' active' : ''}`}
                    onClick={() => setGalleryIndex(index)}
                  >
                    <img src={url} alt={`Мініатюра ${index + 1}`} />
                  </button>
                ))}
              </div>
            ) : null}
          </section>
        </div>
      ) : null}
    </div>
  );
}
