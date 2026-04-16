import React from 'react';

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
            .filter((point: any) => point && typeof point.x === 'number' && typeof point.y === 'number')
            .map((point: any) => ({ x: point.x, y: point.y }))
        )
        .filter((stroke: SignatureStroke) => stroke.length > 0);
    } catch {
      return [];
    }
  }, []);

  const getCanvasPoint = React.useCallback((event: React.PointerEvent<HTMLCanvasElement>): SignaturePoint => {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };

    const rect = canvas.getBoundingClientRect();
    return {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top,
    };
  }, []);

  const drawStroke = React.useCallback((ctx: CanvasRenderingContext2D, stroke: SignatureStroke) => {
    if (stroke.length === 0) return;

    ctx.beginPath();
    ctx.moveTo(stroke[0].x, stroke[0].y);

    for (let i = 1; i < stroke.length; i += 1) {
      ctx.lineTo(stroke[i].x, stroke[i].y);
    }

    ctx.stroke();
  }, []);

  const redraw = React.useCallback((strokes: SignatureStroke[]) => {
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
  }, [drawStroke]);

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
        <button type="button" className="btn-secondary" onClick={clearSignature} disabled={disabled}>
          Vymazat podpis
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
  className = 'form-section',
}) => {
  return (
    <>
      {/* Základní údaje */}
      <section className={className}>
        <h2>Základní údaje</h2>
        <div className="form-row">
          <div className="form-field">
            <label>Příjmení *</label>
            <input 
              type="text" 
              value={form.lastName} 
              onChange={e => onChange('lastName', e.target.value)} 
              required
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Jméno *</label>
            <input 
              type="text" 
              value={form.firstName} 
              onChange={e => onChange('firstName', e.target.value)} 
              required
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Datum narození {!showPhoto ? '*' : ''}</label>
            <input 
              type="date" 
              value={form.dateOfBirth} 
              onChange={e => onChange('dateOfBirth', e.target.value)} 
              required={showPhoto ? false : true}
              disabled={disabled}
            />
          </div>
        </div>
        
        <div className="form-row">
          <div className="form-field">
            <label>Pohlaví {!showPhoto ? '*' : ''}</label>
            <select 
              value={form.gender} 
              onChange={e => onChange('gender', e.target.value)} 
              required={showPhoto ? false : true}
              disabled={disabled}
            >
              <option value="">Vyberte pohlaví</option>
              <option value="M">Muž</option>
              <option value="F">Žena</option>
            </select>
          </div>
          <div className="form-field">
            <label>Váha (kg)</label>
            <input 
              type="number" 
              step="0.1" 
              value={form.weight} 
              onChange={e => onChange('weight', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Výška (cm)</label>
            <input 
              type="number" 
              value={form.height} 
              onChange={e => onChange('height', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>
        
        <div className="form-row">
          <div className="form-field">
            <label>Pojišťovna</label>
            <input 
              type="text" 
              value={form.insuranceCompany} 
              onChange={e => onChange('insuranceCompany', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Email</label>
            <input 
              type="email" 
              value={form.email} 
              onChange={e => onChange('email', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Telefon</label>
            <input 
              type="tel" 
              value={form.phone} 
              onChange={e => onChange('phone', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>
        
        <div className="form-row">
          <div className="form-field">
            <label>Adresa</label>
            <input 
              type="text" 
              value={form.address} 
              onChange={e => onChange('address', e.target.value)} 
              placeholder="Ulice, město"
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>PSČ</label>
            <input 
              type="text" 
              value={form.postalCode} 
              onChange={e => onChange('postalCode', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>
      </section>

      {/* Užívám léky na */}
      <section className={className}>
        <h2>Užívám léky na:</h2>
        <div className="checkbox-grid">
          <label className="checkbox-label"><input type="checkbox" checked={form.medBloodPressure} onChange={e => onChange('medBloodPressure', e.target.checked)} disabled={disabled} />Tlak</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medHeart} onChange={e => onChange('medHeart', e.target.checked)} disabled={disabled} />Srdce</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medCholesterol} onChange={e => onChange('medCholesterol', e.target.checked)} disabled={disabled} />Cholesterol</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medBloodThinners} onChange={e => onChange('medBloodThinners', e.target.checked)} disabled={disabled} />Srážlivost</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medDiabetes} onChange={e => onChange('medDiabetes', e.target.checked)} disabled={disabled} />Cukrovka</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medThyroid} onChange={e => onChange('medThyroid', e.target.checked)} disabled={disabled} />Štítná žláza</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medNerves} onChange={e => onChange('medNerves', e.target.checked)} disabled={disabled} />Nervy</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medPsych} onChange={e => onChange('medPsych', e.target.checked)} disabled={disabled} />Psychika</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medDigestion} onChange={e => onChange('medDigestion', e.target.checked)} disabled={disabled} />Zažívání</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medPain} onChange={e => onChange('medPain', e.target.checked)} disabled={disabled} />Bolest</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medDehydration} onChange={e => onChange('medDehydration', e.target.checked)} disabled={disabled} />Odvodnění</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medBreathing} onChange={e => onChange('medBreathing', e.target.checked)} disabled={disabled} />Dýchání</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medAntibiotics} onChange={e => onChange('medAntibiotics', e.target.checked)} disabled={disabled} />Antibiotika</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medSupplements} onChange={e => onChange('medSupplements', e.target.checked)} disabled={disabled} />Doplňky stravy</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.medAllergies} onChange={e => onChange('medAllergies', e.target.checked)} disabled={disabled} />Alergie</label>
        </div>
      </section>

      {/* Doplňující informace */}
      <section className={className}>
        <h2>Doplňující informace</h2>
        <div className="checkbox-grid">
          <label className="checkbox-label"><input type="checkbox" checked={form.poorSleep} onChange={e => onChange('poorSleep', e.target.checked)} disabled={disabled} />Špatný spánek</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.digestiveIssues} onChange={e => onChange('digestiveIssues', e.target.checked)} disabled={disabled} />Zažívání</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.physicalStress} onChange={e => onChange('physicalStress', e.target.checked)} disabled={disabled} />Fyzická zátěž</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.mentalStress} onChange={e => onChange('mentalStress', e.target.checked)} disabled={disabled} />Psychická zátěž</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.smoking} onChange={e => onChange('smoking', e.target.checked)} disabled={disabled} />Kouření</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.fatigue} onChange={e => onChange('fatigue', e.target.checked)} disabled={disabled} />Pocity únavy</label>
        </div>
        <div className="form-row">
          <div className="form-field">
            <label>Poslední jídlo před (hod.)</label>
            <input 
              type="number" 
              value={form.lastMealHours} 
              onChange={e => onChange('lastMealHours', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>
      </section>

      {/* Další informace o stavu organizmu */}
      <section className={className}>
        <h2>Další informace o stavu organizmu</h2>
        <textarea 
          rows={6} 
          value={form.additionalHealthInfo} 
          onChange={e => onChange('additionalHealthInfo', e.target.value)} 
          placeholder="Další informace o stavu organizmu, které půjdou do komentáře k pacientovi..."
          disabled={disabled}
        />
      </section>

      {/* COVID */}
      <section className={className}>
        <h2>COVID</h2>
        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.hadCovid} onChange={e => onChange('hadCovid', e.target.checked)} disabled={disabled} />Prodělal/a jsem COVID</label>
        </div>
        {form.hadCovid && (
          <div className="form-row">
            <div className="form-field">
              <label>Kdy?</label>
              <input 
                type="text" 
                value={form.covidWhen} 
                onChange={e => onChange('covidWhen', e.target.value)} 
                placeholder="např. 2021, lehký průběh"
                disabled={disabled}
              />
            </div>
          </div>
        )}
        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.covidVaccine} onChange={e => onChange('covidVaccine', e.target.checked)} disabled={disabled} />Vakcinace proti COVID-u</label>
        </div>
        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.vaccinesAfter2023} onChange={e => onChange('vaccinesAfter2023', e.target.checked)} disabled={disabled} />Vakcinace jiné po r.2023</label>
        </div>
      </section>

      {/* Mamární diagnostika */}
      <section className={`${className} optional-section`}>
        <h2>Doplnění jen k mamární diagnostice MEIK (nepovinné)</h2>
        <p className="section-note">Tuto sekci vyplňte pouze pokud je relevantní</p>
        
        <div className="form-row">
          <div className="form-field">
            <label>Kdy poslední menzes</label>
            <input 
              type="text" 
              value={form.lastMenstruationDate} 
              onChange={e => onChange('lastMenstruationDate', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Po kolika dnech se opakuje</label>
            <input 
              type="number" 
              value={form.menstruationCycleDays} 
              onChange={e => onChange('menstruationCycleDays', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Počet let od poslední menzes</label>
            <input 
              type="number" 
              value={form.yearsSinceLastMenstruation} 
              onChange={e => onChange('yearsSinceLastMenstruation', e.target.value)}
              disabled={disabled}
            />
          </div>
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.gaveBirth} onChange={e => onChange('gaveBirth', e.target.checked)} disabled={disabled} />Rodila</label>
          {form.gaveBirth && (
            <>
              <div className="form-field">
                <label>Kolikrát</label>
                <input 
                  type="number" 
                  value={form.birthCount} 
                  onChange={e => onChange('birthCount', e.target.value)}
                  disabled={disabled}
                />
              </div>
              <div className="form-field">
                <label>Kdy</label>
                <input 
                  type="text" 
                  value={form.birthWhen} 
                  onChange={e => onChange('birthWhen', e.target.value)} 
                  placeholder="např. 2015, 2018"
                  disabled={disabled}
                />
              </div>
            </>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.breastfed} onChange={e => onChange('breastfed', e.target.checked)} disabled={disabled} />Kojila</label>
          {form.breastfed && (
            <div className="form-field">
              <label>Počet měsíců</label>
              <input 
                type="number" 
                value={form.breastfeedingMonths} 
                onChange={e => onChange('breastfeedingMonths', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.breastfeedingInflammation} onChange={e => onChange('breastfeedingInflammation', e.target.checked)} disabled={disabled} />Záněty při kojení?</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.endedWithInflammation} onChange={e => onChange('endedWithInflammation', e.target.checked)} disabled={disabled} />Končilo kojení zánětem?</label>
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.contraception} onChange={e => onChange('contraception', e.target.checked)} disabled={disabled} />Antikoncepce</label>
          {form.contraception && (
            <div className="form-field">
              <label>Jak dlouho?</label>
              <input 
                type="text" 
                value={form.contraceptionDuration} 
                onChange={e => onChange('contraceptionDuration', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.estrogen} onChange={e => onChange('estrogen', e.target.checked)} disabled={disabled} />Estrogen</label>
          {form.estrogen && (
            <div className="form-field">
              <label>Druh?</label>
              <input 
                type="text" 
                value={form.estrogenType} 
                onChange={e => onChange('estrogenType', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.interruption} onChange={e => onChange('interruption', e.target.checked)} disabled={disabled} />Interrupce</label>
          {form.interruption && (
            <div className="form-field">
              <label>Počet?</label>
              <input 
                type="number" 
                value={form.interruptionCount} 
                onChange={e => onChange('interruptionCount', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
          <label className="checkbox-label"><input type="checkbox" checked={form.miscarriage} onChange={e => onChange('miscarriage', e.target.checked)} disabled={disabled} />Potrat</label>
          {form.miscarriage && (
            <div className="form-field">
              <label>Počet?</label>
              <input 
                type="number" 
                value={form.miscarriageCount} 
                onChange={e => onChange('miscarriageCount', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.breastInjury} onChange={e => onChange('breastInjury', e.target.checked)} disabled={disabled} />Úraz prsu?</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.mammogram} onChange={e => onChange('mammogram', e.target.checked)} disabled={disabled} />RTG Mamograf?</label>
          {form.mammogram && (
            <div className="form-field">
              <label>Počet?</label>
              <input 
                type="number" 
                value={form.mammogramCount} 
                onChange={e => onChange('mammogramCount', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.breastBiopsy} onChange={e => onChange('breastBiopsy', e.target.checked)} disabled={disabled} />Biopsie prsu?</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.breastImplants} onChange={e => onChange('breastImplants', e.target.checked)} disabled={disabled} />Impl?</label>
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.breastSurgery} onChange={e => onChange('breastSurgery', e.target.checked)} disabled={disabled} />Operace prsu?</label>
          {form.breastSurgery && (
            <div className="form-field">
              <label>Jaká?</label>
              <input 
                type="text" 
                value={form.breastSurgeryType} 
                onChange={e => onChange('breastSurgeryType', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>

        <div className="form-row">
          <label className="checkbox-label"><input type="checkbox" checked={form.familyTumors} onChange={e => onChange('familyTumors', e.target.checked)} disabled={disabled} />Nádory v rodině?</label>
          {form.familyTumors && (
            <div className="form-field">
              <label>Druh?</label>
              <input 
                type="text" 
                value={form.familyTumorType} 
                onChange={e => onChange('familyTumorType', e.target.value)}
                disabled={disabled}
              />
            </div>
          )}
        </div>
      </section>

      {/* Potvrzení správnosti a Smluvní podmínky */}
      <section className={`${className} terms-section`}>
        <h2>Potvrzení správnosti vyplněných údajů</h2>
        <div className="form-row">
          <label className="checkbox-label">
            <input 
              type="checkbox" 
              checked={form.confirmAccuracy} 
              onChange={e => onChange('confirmAccuracy', e.target.checked)} 
              required
              disabled={disabled}
            />
            Potvrzuji správnost vyplnění výše uvedených údajů *
          </label>
        </div>
        
        <h2>Smluvní podmínky</h2>
        <div className="terms-text">
          <ol>
            <li>Klient souhlasí se zpracováním svých osobních údajů Centrem Preventivní Medicíny (dále CPM).</li>
            <li>Klient, při pobytu v CPM, respektuje organizační pokyny personálu CPM.</li>
            <li>CPM zachází s osobními údaji klientů podle platných zákonů EU a ČR, které to upravují.</li>
            <li>CPM uchovává dokumenty, výsledky různých vyšetření v CPM nebo které Klient CPM předal.</li>
            <li>CPM používá výsledky vyšetření Klienta jen k dalšímu odbornému posouzení, eventuálně k další výzkumné činnosti nebo ke statistice v navazujících databázích. Ve všech těchto případech respektuje anonymitu klientů (není možno spojit osobní údaje s výsledkem vyšetření).</li>
            <li>CPM může informovat Klienta o změně v organizaci poskytování svých služeb tím způsobem, který si při objednání služeb zvolil Klient sám.</li>
            <li>Klienta CPM vyškrtne z aktuální evidence jen na základě jeho písemné žádosti nebo při úmrtí.</li>
          </ol>
        </div>
        <div className="form-row">
          <label className="checkbox-label">
            <input 
              type="checkbox" 
              checked={form.termsAccepted} 
              onChange={e => onChange('termsAccepted', e.target.checked)} 
              required
              disabled={disabled}
            />
            Se smluvními podmínkami CPM souhlasím *
          </label>
        </div>

        <div className="form-row">
          <div className="form-field">
            <label>Kde</label>
            <input 
              type="text" 
              value={form.signaturePlace} 
              onChange={e => onChange('signaturePlace', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Datum</label>
            <input 
              type="date" 
              value={form.signatureDate} 
              onChange={e => onChange('signatureDate', e.target.value)}
              disabled={disabled}
            />
          </div>
          <div className="form-field">
            <label>Podpis klienta</label>
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
