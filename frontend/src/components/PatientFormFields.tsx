import React from 'react';
import { useTranslation } from 'react-i18next';

export interface PatientFormData {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  gender: string;
  weight: string;
  height: string;
  insuranceCompany: string;
  email: string;
  phone: string;
  address: string;
  postalCode: string;

  medBloodPressure: boolean;
  medHeart: boolean;
  medCholesterol: boolean;
  medBloodThinners: boolean;
  medDiabetes: boolean;
  medThyroid: boolean;
  medNerves: boolean;
  medPsych: boolean;
  medDigestion: boolean;
  medPain: boolean;
  medDehydration: boolean;
  medBreathing: boolean;
  medAntibiotics: boolean;
  medSupplements: boolean;
  medAllergies: boolean;

  poorSleep: boolean;
  digestiveIssues: boolean;
  physicalStress: boolean;
  mentalStress: boolean;
  smoking: boolean;
  fatigue: boolean;
  lastMealHours: string;

  confirmAccuracy: boolean;
  termsAccepted: boolean;
  signaturePlace: string;
  signatureDate: string;
  signatureVector: string;

  additionalHealthInfo: string;

  hadCovid: boolean;
  covidWhen: string;
  covidVaccine: boolean;
  vaccinesAfter2023: boolean;

  lastMenstruationDate: string;
  menstruationCycleDays: string;
  yearsSinceLastMenstruation: string;
  gaveBirth: boolean;
  birthCount: string;
  birthWhen: string;
  breastfed: boolean;
  breastfeedingMonths: string;
  breastfeedingInflammation: boolean;
  endedWithInflammation: boolean;
  contraception: boolean;
  contraceptionDuration: string;
  estrogen: boolean;
  estrogenType: string;
  interruption: boolean;
  interruptionCount: string;
  miscarriage: boolean;
  miscarriageCount: string;
  breastInjury: boolean;
  mammogram: boolean;
  mammogramCount: string;
  breastBiopsy: boolean;
  breastImplants: boolean;
  breastSurgery: boolean;
  breastSurgeryType: string;
  familyTumors: boolean;
  familyTumorType: string;
}

interface PatientFormFieldsProps {
  form: PatientFormData;
  onChange: (field: keyof PatientFormData, value: any) => void;
  showPhoto?: boolean;
  disabled?: boolean;
  className?: string;
}

interface SignaturePoint {
  x: number;
  y: number;
}

type SignatureStroke = SignaturePoint[];

const SignaturePad: React.FC<{
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}> = ({ value, disabled, onChange }) => {
  const { t } = useTranslation();
  const canvasRef = React.useRef<HTMLCanvasElement>(null);
  const drawingRef = React.useRef(false);
  const currentStrokeRef = React.useRef<SignatureStroke>([]);

  const parseStrokes = React.useCallback((raw: string): SignatureStroke[] => {
    if (!raw) return [];
    try {
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      return parsed
        .filter(Array.isArray)
        .map((stroke: any[]) =>
          stroke
            .filter(
              (point: any) => point && typeof point.x === 'number' && typeof point.y === 'number'
            )
            .map((point: any) => ({ x: point.x, y: point.y }))
        )
        .filter((stroke: SignatureStroke) => stroke.length > 0);
    } catch {
      return [];
    }
  }, []);

  const getCanvasPoint = React.useCallback(
    (event: React.PointerEvent<HTMLCanvasElement>): SignaturePoint => {
      const canvas = canvasRef.current;
      if (!canvas) return { x: 0, y: 0 };

      const rect = canvas.getBoundingClientRect();
      return {
        x: event.clientX - rect.left,
        y: event.clientY - rect.top
      };
    },
    []
  );

  const drawStroke = React.useCallback((ctx: CanvasRenderingContext2D, stroke: SignatureStroke) => {
    if (stroke.length === 0) return;

    const firstPoint = stroke[0];
    if (!firstPoint) return;

    ctx.beginPath();
    ctx.moveTo(firstPoint.x, firstPoint.y);

    for (let i = 1; i < stroke.length; i += 1) {
      const point = stroke[i];
      if (!point) continue;
      ctx.lineTo(point.x, point.y);
    }

    ctx.stroke();
  }, []);

  const redraw = React.useCallback(
    (strokes: SignatureStroke[]) => {
      const canvas = canvasRef.current;
      if (!canvas) return;

      const context = canvas.getContext('2d');
      if (!context) return;

      context.clearRect(0, 0, canvas.width, canvas.height);
      context.lineWidth = 2;
      context.lineCap = 'round';
      context.lineJoin = 'round';
      context.strokeStyle = '#1f2937';

      strokes.forEach((stroke) => drawStroke(context, stroke));
    },
    [drawStroke]
  );

  React.useEffect(() => {
    redraw(parseStrokes(value));
  }, [value, parseStrokes, redraw]);

  const handlePointerDown = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (disabled) return;
    drawingRef.current = true;
    currentStrokeRef.current = [getCanvasPoint(event)];
  };

  const handlePointerMove = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawingRef.current || disabled) return;

    const point = getCanvasPoint(event);
    currentStrokeRef.current = [...currentStrokeRef.current, point];

    const strokes = [...parseStrokes(value), currentStrokeRef.current];
    redraw(strokes);
  };

  const finishStroke = () => {
    if (!drawingRef.current || disabled) return;
    drawingRef.current = false;

    const completedStroke = currentStrokeRef.current;
    currentStrokeRef.current = [];

    if (completedStroke.length === 0) return;

    const strokes = [...parseStrokes(value), completedStroke];
    onChange(JSON.stringify(strokes));
  };

  const clearSignature = () => {
    if (disabled) return;
    onChange('');
  };

  return (
    <div className="signature-pad-wrapper">
      <canvas
        ref={canvasRef}
        width={640}
        height={220}
        className={`signature-pad-canvas${disabled ? ' disabled' : ''}`}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={finishStroke}
        onPointerLeave={finishStroke}
      />
      <div className="signature-pad-actions">
        <button
          type="button"
          className="btn-secondary"
          onClick={clearSignature}
          disabled={disabled}
        >
          {t('patientForm.clearSignature')}
        </button>
      </div>
    </div>
  );
};

export const PatientFormFields: React.FC<PatientFormFieldsProps> = ({
  form,
  onChange,
  showPhoto = false,
  disabled = false,
  className = 'form-section'
}) => {
  const { t } = useTranslation();

  return (
    <>
      {/* Základní údaje */}
      <section className={className}>
        <h2>{t('patientForm.basicInfo')}</h2>
        <div className="form-row">
          <div className="form-field">
            <label>{t('patientForm.lastName')} *</label>
            <input
              type="text"
              value={form.lastName}
              onChange={(e) => onChange('lastName', e.target.value)}
              required
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.firstName')} *</label>
            <input
              type="text"
              value={form.firstName}
              onChange={(e) => onChange('firstName', e.target.value)}
              required
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>
              {t('patientForm.dateOfBirth')}
              {!showPhoto ? ' *' : ''}
            </label>
            <input
              type="date"
              value={form.dateOfBirth}
              onChange={(e) => onChange('dateOfBirth', e.target.value)}
              required={showPhoto ? false : true}
              disabled={disabled}
            />
          </div>
        </div>

        <div className="form-row">
          <div className="form-field">
            <label>
              {t('patientForm.gender')}
              {!showPhoto ? ' *' : ''}
            </label>
            <select
              value={form.gender}
              onChange={(e) => onChange('gender', e.target.value)}
              required={showPhoto ? false : true}
              disabled={disabled}
            >
              <option value="">{t('patientForm.selectGender')}</option>
              <option value="M">{t('patientForm.male')}</option>
              <option value="F">{t('patientForm.female')}</option>
            </select>
          </div>
          <div className="form-field">
            <label>{t('patientForm.weight')}</label>
            <input
              type="number"
              step="0.1"
              value={form.weight}
              onChange={(e) => onChange('weight', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.height')}</label>
            <input
              type="number"
              value={form.height}
              onChange={(e) => onChange('height', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>

        <div className="form-row">
          <div className="form-field">
            <label>{t('patientForm.insuranceCompany')}</label>
            <input
              type="text"
              value={form.insuranceCompany}
              onChange={(e) => onChange('insuranceCompany', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.email')}</label>
            <input
              type="email"
              value={form.email}
              onChange={(e) => onChange('email', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.phone')}</label>
            <input
              type="tel"
              value={form.phone}
              onChange={(e) => onChange('phone', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>

        <div className="form-row">
          <div className="form-field">
            <label>{t('patientForm.address')}</label>
            <input
              type="text"
              value={form.address}
              onChange={(e) => onChange('address', e.target.value)}
              placeholder={t('patientForm.addressPlaceholder')}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.postalCode')}</label>
            <input
              type="text"
              value={form.postalCode}
              onChange={(e) => onChange('postalCode', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>
      </section>

      {/* Užívám léky na */}
      <section className={className}>
        <h2>{t('patientForm.medications')}</h2>
        <div className="checkbox-grid">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medBloodPressure}
              onChange={(e) => onChange('medBloodPressure', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medBloodPressure')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medHeart}
              onChange={(e) => onChange('medHeart', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medHeart')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medCholesterol}
              onChange={(e) => onChange('medCholesterol', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medCholesterol')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medBloodThinners}
              onChange={(e) => onChange('medBloodThinners', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medBloodThinners')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medDiabetes}
              onChange={(e) => onChange('medDiabetes', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medDiabetes')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medThyroid}
              onChange={(e) => onChange('medThyroid', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medThyroid')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medNerves}
              onChange={(e) => onChange('medNerves', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medNerves')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medPsych}
              onChange={(e) => onChange('medPsych', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medPsych')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medDigestion}
              onChange={(e) => onChange('medDigestion', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medDigestion')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medPain}
              onChange={(e) => onChange('medPain', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medPain')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medDehydration}
              onChange={(e) => onChange('medDehydration', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medDehydration')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medBreathing}
              onChange={(e) => onChange('medBreathing', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medBreathing')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medAntibiotics}
              onChange={(e) => onChange('medAntibiotics', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medAntibiotics')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medSupplements}
              onChange={(e) => onChange('medSupplements', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medSupplements')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.medAllergies}
              onChange={(e) => onChange('medAllergies', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.medAllergies')}
          </label>
        </div>
      </section>

      {/* Doplňující informace */}
      <section className={className}>
        <h2>{t('patientForm.additionalInfo')}</h2>
        <div className="checkbox-grid">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.poorSleep}
              onChange={(e) => onChange('poorSleep', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.poorSleep')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.digestiveIssues}
              onChange={(e) => onChange('digestiveIssues', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.digestiveIssues')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.physicalStress}
              onChange={(e) => onChange('physicalStress', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.physicalStress')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.mentalStress}
              onChange={(e) => onChange('mentalStress', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.mentalStress')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.smoking}
              onChange={(e) => onChange('smoking', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.smoking')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.fatigue}
              onChange={(e) => onChange('fatigue', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.fatigue')}
          </label>
        </div>
        <div className="form-row">
          <div className="form-field">
            <label>{t('patientForm.lastMealHours')}</label>
            <input
              type="number"
              value={form.lastMealHours}
              onChange={(e) => onChange('lastMealHours', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>
      </section>

      {/* Další informace o stavu organizmu */}
      <section className={className}>
        <h2>{t('patientForm.healthStatusInfo')}</h2>
        <textarea
          rows={6}
          value={form.additionalHealthInfo}
          onChange={(e) => onChange('additionalHealthInfo', e.target.value)}
          placeholder={t('patientForm.healthStatusPlaceholder')}
          disabled={disabled}
        />
      </section>

      {/* COVID */}
      <section className={className}>
        <h2>{t('patientForm.covid')}</h2>
        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.hadCovid}
              onChange={(e) => onChange('hadCovid', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.hadCovid')}
          </label>
        </div>
        {form.hadCovid && (
          <div className="form-row">
            <div className="form-field">
              <label>{t('patientForm.covidWhen')}</label>
              <input
                type="text"
                value={form.covidWhen}
                onChange={(e) => onChange('covidWhen', e.target.value)}
                placeholder={t('patientForm.covidWhenPlaceholder')}
                disabled={disabled}
              />
            </div>
          </div>
        )}
        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.covidVaccine}
              onChange={(e) => onChange('covidVaccine', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.covidVaccine')}
          </label>
        </div>
        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.vaccinesAfter2023}
              onChange={(e) => onChange('vaccinesAfter2023', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.vaccinesAfter2023')}
          </label>
        </div>
      </section>

      {/* Mamární diagnostika */}
      <section className={`${className} optional-section`}>
        <h2>{t('patientForm.mammarySection')}</h2>
        <p className="section-note">{t('patientForm.mammarySectionNote')}</p>

        <div className="form-row">
          <div className="form-field">
            <label>{t('patientForm.lastMenstruationDate')}</label>
            <input
              type="text"
              value={form.lastMenstruationDate}
              onChange={(e) => onChange('lastMenstruationDate', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.menstruationCycleDays')}</label>
            <input
              type="number"
              value={form.menstruationCycleDays}
              onChange={(e) => onChange('menstruationCycleDays', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.yearsSinceLastMenstruation')}</label>
            <input
              type="number"
              value={form.yearsSinceLastMenstruation}
              onChange={(e) => onChange('yearsSinceLastMenstruation', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.gaveBirth}
              onChange={(e) => onChange('gaveBirth', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.gaveBirth')}
          </label>
          {form.gaveBirth && (
            <>
              <div className="form-field">
                <label>{t('patientForm.birthCount')}</label>
                <input
                  type="number"
                  value={form.birthCount}
                  onChange={(e) => onChange('birthCount', e.target.value)}
                  disabled={disabled}
                />
              </div>
              <div className="form-field">
                <label>{t('patientForm.birthWhen')}</label>
                <input
                  type="text"
                  value={form.birthWhen}
                  onChange={(e) => onChange('birthWhen', e.target.value)}
                  placeholder={t('patientForm.birthWhenPlaceholder')}
                  disabled={disabled}
                />
              </div>
            </>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.breastfed}
              onChange={(e) => onChange('breastfed', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.breastfed')}
          </label>
          {form.breastfed && (
            <div className="form-field">
              <label>{t('patientForm.breastfeedingMonths')}</label>
              <input
                type="number"
                value={form.breastfeedingMonths}
                onChange={(e) => onChange('breastfeedingMonths', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.breastfeedingInflammation}
              onChange={(e) => onChange('breastfeedingInflammation', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.breastfeedingInflammation')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.endedWithInflammation}
              onChange={(e) => onChange('endedWithInflammation', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.endedWithInflammation')}
          </label>
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.contraception}
              onChange={(e) => onChange('contraception', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.contraception')}
          </label>
          {form.contraception && (
            <div className="form-field">
              <label>{t('patientForm.contraceptionDuration')}</label>
              <input
                type="text"
                value={form.contraceptionDuration}
                onChange={(e) => onChange('contraceptionDuration', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.estrogen}
              onChange={(e) => onChange('estrogen', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.estrogen')}
          </label>
          {form.estrogen && (
            <div className="form-field">
              <label>{t('patientForm.type')}</label>
              <input
                type="text"
                value={form.estrogenType}
                onChange={(e) => onChange('estrogenType', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.interruption}
              onChange={(e) => onChange('interruption', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.interruption')}
          </label>
          {form.interruption && (
            <div className="form-field">
              <label>{t('patientForm.count')}</label>
              <input
                type="number"
                value={form.interruptionCount}
                onChange={(e) => onChange('interruptionCount', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.miscarriage}
              onChange={(e) => onChange('miscarriage', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.miscarriage')}
          </label>
          {form.miscarriage && (
            <div className="form-field">
              <label>{t('patientForm.count')}</label>
              <input
                type="number"
                value={form.miscarriageCount}
                onChange={(e) => onChange('miscarriageCount', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.breastInjury}
              onChange={(e) => onChange('breastInjury', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.breastInjury')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.mammogram}
              onChange={(e) => onChange('mammogram', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.mammogram')}
          </label>
          {form.mammogram && (
            <div className="form-field">
              <label>{t('patientForm.count')}</label>
              <input
                type="number"
                value={form.mammogramCount}
                onChange={(e) => onChange('mammogramCount', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.breastBiopsy}
              onChange={(e) => onChange('breastBiopsy', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.breastBiopsy')}
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.breastImplants}
              onChange={(e) => onChange('breastImplants', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.breastImplants')}
          </label>
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.breastSurgery}
              onChange={(e) => onChange('breastSurgery', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.breastSurgery')}
          </label>
          {form.breastSurgery && (
            <div className="form-field">
              <label>{t('patientForm.breastSurgeryType')}</label>
              <input
                type="text"
                value={form.breastSurgeryType}
                onChange={(e) => onChange('breastSurgeryType', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.familyTumors}
              onChange={(e) => onChange('familyTumors', e.target.checked)}
              disabled={disabled}
            />
            {t('patientForm.familyTumors')}
          </label>
          {form.familyTumors && (
            <div className="form-field">
              <label>{t('patientForm.type')}</label>
              <input
                type="text"
                value={form.familyTumorType}
                onChange={(e) => onChange('familyTumorType', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>
      </section>

      {/* Potvrzení správnosti a Smluvní podmínky */}
      <section className={`${className} terms-section`}>
        <h2>{t('patientForm.accuracyTitle')}</h2>
        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.confirmAccuracy}
              onChange={(e) => onChange('confirmAccuracy', e.target.checked)}
              required
              disabled={disabled}
            />
            {t('patientForm.accuracyConfirmation')} *
          </label>
        </div>

        <h2>{t('patientForm.termsTitle')}</h2>
        <div className="terms-text">
          <ol>
            <li>{t('patientForm.termsItem1')}</li>
            <li>{t('patientForm.termsItem2')}</li>
            <li>{t('patientForm.termsItem3')}</li>
            <li>{t('patientForm.termsItem4')}</li>
            <li>{t('patientForm.termsItem5')}</li>
            <li>{t('patientForm.termsItem6')}</li>
            <li>{t('patientForm.termsItem7')}</li>
          </ol>
        </div>
        <div className="form-row">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={form.termsAccepted}
              onChange={(e) => onChange('termsAccepted', e.target.checked)}
              required
              disabled={disabled}
            />
            {t('patientForm.termsAcceptance')} *
          </label>
        </div>

        <div className="form-row">
          <div className="form-field">
            <label>{t('patientForm.signaturePlace')}</label>
            <input
              type="text"
              value={form.signaturePlace}
              onChange={(e) => onChange('signaturePlace', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.signatureDate')}</label>
            <input
              type="date"
              value={form.signatureDate}
              onChange={(e) => onChange('signatureDate', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>{t('patientForm.signature')}</label>
            <SignaturePad
              value={form.signatureVector}
              disabled={disabled}
              onChange={(nextValue) => onChange('signatureVector', nextValue)}
            />
          </div>
        </div>
      </section>
    </>
  );
};
