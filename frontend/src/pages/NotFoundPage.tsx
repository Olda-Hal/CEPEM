import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import './NotFoundPage.css';

export const NotFoundPage: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div className="not-found-page">
      <h1>{t('notFound.title')}</h1>
      <p>{t('notFound.description')}</p>
      <Link to="/dashboard" className="btn btn-primary">
        {t('notFound.backHome')}
      </Link>
    </div>
  );
};

export default NotFoundPage;
